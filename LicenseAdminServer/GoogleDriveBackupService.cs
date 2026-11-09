using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class GoogleDriveBackupService
{
    private const string RefreshTokenConfigKey = "GoogleDriveRefreshToken";
    private const string ConnectedEmailConfigKey = "GoogleDriveConnectedEmail";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UploadGates = new(StringComparer.Ordinal);
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    private static string ClientId => Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID")?.Trim() ?? string.Empty;
    private static string ClientSecret => Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET")?.Trim() ?? string.Empty;
    private static string RedirectUri => Environment.GetEnvironmentVariable("GOOGLE_DRIVE_REDIRECT_URI")?.Trim() ?? string.Empty;

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        Uri.TryCreate(RedirectUri, UriKind.Absolute, out Uri? redirectUri) &&
        redirectUri.Scheme == Uri.UriSchemeHttps;

    public static bool IsConnected(string dbPath) =>
        IsConfigured && !string.IsNullOrWhiteSpace(Db.GetSecureConfig(dbPath, RefreshTokenConfigKey));

    public static string GetConnectedEmail(string dbPath) =>
        Db.GetConfigValue(dbPath, ConnectedEmailConfigKey) ?? string.Empty;

    public static string BuildAuthorizationUrl(string state)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Google Drive OAuth server sozlamalari kiritilmagan.");
        }

        var query = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email https://www.googleapis.com/auth/drive.file",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };
        string encoded = string.Join("&", query.Select(x =>
            $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return "https://accounts.google.com/o/oauth2/v2/auth?" + encoded;
    }

    public static async Task<string> ExchangeCodeAndSaveAsync(string dbPath, string code)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Google Drive OAuth server sozlamalari kiritilmagan.");
        }

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["redirect_uri"] = RedirectUri,
                ["grant_type"] = "authorization_code"
            })
        };
        using var tokenResponse = await Http.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google OAuth token almashinuvi muvaffaqiyatsiz tugadi ({(int)tokenResponse.StatusCode}).");
        }

        OAuthTokenResponse? tokens = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        if (tokens == null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            throw new InvalidOperationException("Google refresh token bermadi. Qayta ulanishga ruxsat bering.");
        }

        Db.SetSecureConfig(dbPath, RefreshTokenConfigKey, tokens.RefreshToken);
        string email = await GetAccountEmailAsync(tokens.AccessToken) ?? string.Empty;
        Db.SetConfigValue(dbPath, ConnectedEmailConfigKey, email);
        return email;
    }

    public static void Disconnect(string dbPath)
    {
        Db.DeleteConfig(dbPath, RefreshTokenConfigKey);
        Db.DeleteConfig(dbPath, ConnectedEmailConfigKey);
    }

    public static async Task<GoogleDriveUploadResult> UploadAndPruneAsync(
        string dbPath,
        string licenseKey,
        string deviceId,
        byte[] encryptedBackup)
    {
        if (!IsConnected(dbPath))
        {
            throw new InvalidOperationException("Google Drive hisob ulanmagan.");
        }

        string refreshToken = Db.GetSecureConfig(dbPath, RefreshTokenConfigKey)
            ?? throw new InvalidOperationException("Google Drive refresh token topilmadi.");
        string accessToken = await RefreshAccessTokenAsync(refreshToken);
        string backupKey = GetBackupKey(licenseKey, deviceId);
        SemaphoreSlim gate = UploadGates.GetOrAdd(backupKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            string remoteName = $"OsonTrack_{backupKey[..16]}_{DateTime.UtcNow:yyyyMMdd}.osonbackup";

            DriveFileResponse? uploaded = await UploadFileAsync(accessToken, remoteName, backupKey, encryptedBackup);
            if (string.IsNullOrWhiteSpace(uploaded?.Id))
            {
                throw new InvalidOperationException("Google Drive yangi zaxira fayliga ID qaytarmadi.");
            }

            byte[] uploadedBytes = await DownloadFileContentAsync(accessToken, uploaded.Id);
            try
            {
                byte[] expectedHash = SHA256.HashData(encryptedBackup);
                byte[] actualHash = SHA256.HashData(uploadedBytes);
                bool matches = uploadedBytes.LongLength == encryptedBackup.LongLength &&
                               CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
                CryptographicOperations.ZeroMemory(expectedHash);
                CryptographicOperations.ZeroMemory(actualHash);
                if (!matches)
                {
                    throw new InvalidDataException("Google Drive yuklangan backup yaxlitligini tasdiqlay olmadi.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(uploadedBytes);
            }

            List<DriveFileResponse> storedFiles = await ListBackupFilesAsync(accessToken, backupKey);
            int removed = 0;
            foreach (DriveFileResponse oldFile in storedFiles.Where(file =>
                         !string.Equals(file.Id, uploaded.Id, StringComparison.Ordinal)))
            {
                if (!string.IsNullOrWhiteSpace(oldFile.Id))
                {
                    await DeleteFileAsync(accessToken, oldFile.Id);
                }
                removed++;
            }

            return new GoogleDriveUploadResult(uploaded.Id, removed, remoteName);
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task<GoogleDriveDownloadResult?> DownloadLatestAsync(
        string dbPath,
        string licenseKey,
        string deviceId)
    {
        if (!IsConnected(dbPath))
        {
            throw new InvalidOperationException("Google Drive hisob ulanmagan.");
        }

        string refreshToken = Db.GetSecureConfig(dbPath, RefreshTokenConfigKey)
            ?? throw new InvalidOperationException("Google Drive refresh token topilmadi.");
        string accessToken = await RefreshAccessTokenAsync(refreshToken);
        string backupKey = GetBackupKey(licenseKey, deviceId);
        List<DriveFileResponse> files = await ListBackupFilesAsync(accessToken, backupKey);
        DriveFileResponse? latest = files
            .OrderByDescending(x => DateTime.TryParse(x.CreatedTime, out DateTime created) ? created : DateTime.MinValue)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(latest?.Id))
        {
            return null;
        }

        byte[] content = await DownloadFileContentAsync(accessToken, latest.Id);
        if (!IsEncryptedBackup(content))
        {
            CryptographicOperations.ZeroMemory(content);
            throw new InvalidDataException("Google Drive'dagi zaxira fayli noto'g'ri formatda.");
        }

        return new GoogleDriveDownloadResult(content, Path.GetFileName(latest.Name));
    }

    private static async Task<byte[]> DownloadFileContentAsync(string accessToken, string fileId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(fileId) + "?alt=media");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Drive zaxirasi yuklab olinmadi ({(int)response.StatusCode}).");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            })
        };
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google OAuth access token yangilanmadi ({(int)response.StatusCode}).");
        }

        OAuthTokenResponse? tokens = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        return string.IsNullOrWhiteSpace(tokens?.AccessToken)
            ? throw new InvalidOperationException("Google access token qaytarmadi.")
            : tokens.AccessToken;
    }

    private static async Task<string?> GetAccountEmailAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        UserInfoResponse? user = await response.Content.ReadFromJsonAsync<UserInfoResponse>();
        return user?.Email ?? string.Empty;
    }

    private static async Task<DriveFileResponse?> UploadFileAsync(
        string accessToken,
        string remoteName,
        string backupKey,
        byte[] encryptedBackup)
    {
        var metadata = new Dictionary<string, object>
        {
            ["name"] = remoteName,
            ["mimeType"] = "application/octet-stream",
            ["appProperties"] = new Dictionary<string, string>
            {
                ["osontrackBackupKey"] = backupKey
            }
        };
        using var content = new MultipartContent("related");
        content.Add(new StringContent(JsonSerializer.Serialize(metadata), Encoding.UTF8, "application/json"));
        var filePart = new ByteArrayContent(encryptedBackup);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(filePart);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,size")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Drive zaxira faylini saqlamadi ({(int)response.StatusCode}).");
        }

        DriveFileResponse? result = await response.Content.ReadFromJsonAsync<DriveFileResponse>();
        if (string.IsNullOrWhiteSpace(result?.Id) ||
            !long.TryParse(result.Size, out long uploadedSize) ||
            uploadedSize != encryptedBackup.LongLength)
        {
            throw new InvalidOperationException("Google Drive yuklangan zaxira hajmini tasdiqlamadi.");
        }

        return result;
    }

    private static async Task<List<DriveFileResponse>> ListBackupFilesAsync(string accessToken, string backupKey)
    {
        string query = $"appProperties has {{ key='osontrackBackupKey' and value='{backupKey}' }} and trashed=false";
        var files = new List<DriveFileResponse>();
        string? pageToken = null;
        do
        {
            string url = "https://www.googleapis.com/drive/v3/files?q=" + Uri.EscapeDataString(query) +
                         "&fields=nextPageToken,files(id,name,size,createdTime)&pageSize=1000";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                url += "&pageToken=" + Uri.EscapeDataString(pageToken);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Google Drive eski zaxiralar ro'yxatini bermadi ({(int)response.StatusCode}).");
            }

            DriveFilesResponse? result = await response.Content.ReadFromJsonAsync<DriveFilesResponse>();
            if (result?.Files != null)
            {
                files.AddRange(result.Files);
            }
            pageToken = result?.NextPageToken;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return files;
    }

    private static async Task DeleteFileAsync(string accessToken, string fileId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(fileId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Google Drive eski zaxira faylini o'chirmadi ({(int)response.StatusCode}).");
        }
    }

    private static string GetBackupKey(string licenseKey, string deviceId)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{licenseKey}\0{deviceId}");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsEncryptedBackup(byte[] data)
    {
        byte[] header = Encoding.ASCII.GetBytes("OSONBK01");
        return data.Length >= header.Length + 12 + 16 &&
               data.AsSpan(0, header.Length).SequenceEqual(header);
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class UserInfoResponse
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class DriveFileResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;

        [JsonPropertyName("createdTime")]
        public string CreatedTime { get; set; } = string.Empty;
    }

    private sealed class DriveFilesResponse
    {
        [JsonPropertyName("files")]
        public List<DriveFileResponse> Files { get; set; } = new();

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
    }
}

internal sealed record GoogleDriveUploadResult(string FileId, int RemovedOldFiles, string FileName);
internal sealed record GoogleDriveDownloadResult(byte[] Content, string FileName);
