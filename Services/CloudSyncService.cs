using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SantexnikaSRM.Data;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Services
{
    public sealed class CloudSyncService
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(3)
        };

        private readonly DatabaseHelper _db = new DatabaseHelper();
        private readonly BackupService _backupService = new BackupService();
        private readonly CloudBackupCryptoService _backupCrypto = new CloudBackupCryptoService();

        public async Task RunAsync(LocalActivationRecord activation, string appVersion)
        {
            if (activation == null || string.IsNullOrWhiteSpace(activation.ServerUrl))
            {
                return;
            }

            await ApplyPendingPasswordResetsAsync(activation);
            await SendUsersSnapshotAsync(activation, appVersion);
            await UploadMonthlyBackupIfEnabledAsync(activation, appVersion);
        }

        public Task ApplyPendingPasswordResetsNowAsync(LocalActivationRecord activation)
        {
            if (activation == null || string.IsNullOrWhiteSpace(activation.ServerUrl))
            {
                return Task.CompletedTask;
            }

            return ApplyPendingPasswordResetsAsync(activation);
        }

        public Task<bool> UploadMonthlyBackupIfEnabledAsync(LocalActivationRecord activation, string appVersion)
        {
            if (activation == null ||
                string.IsNullOrWhiteSpace(activation.ServerUrl) ||
                !_backupCrypto.IsEnabled())
            {
                return Task.FromResult(false);
            }

            return UploadBackupAsync(activation, appVersion, force: false);
        }

        public Task<bool> UploadBackupNowIfEnabledAsync(LocalActivationRecord activation, string appVersion)
        {
            if (activation == null ||
                string.IsNullOrWhiteSpace(activation.ServerUrl) ||
                !_backupCrypto.IsEnabled())
            {
                return Task.FromResult(false);
            }

            return UploadBackupAsync(activation, appVersion, force: true);
        }

        public async Task<byte[]> DownloadLatestBackupAsync(LocalActivationRecord activation)
        {
            string url = $"{activation.ServerUrl.TrimEnd('/')}/api/client/backups/latest?licenseKey={Uri.EscapeDataString(activation.LicenseKey)}&deviceId={Uri.EscapeDataString(activation.DeviceId)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activation.Token ?? string.Empty);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Google Drive'da bu qurilma uchun zaxira topilmadi."
                        : $"Google Drive zaxirasi yuklab olinmadi ({(int)response.StatusCode}).");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task ApplyPendingPasswordResetsAsync(LocalActivationRecord activation)
        {
            try
            {
                string baseUrl = activation.ServerUrl.TrimEnd('/');
                string url = $"{baseUrl}/api/client/password-resets/pending?licenseKey={Uri.EscapeDataString(activation.LicenseKey)}&deviceId={Uri.EscapeDataString(activation.DeviceId)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activation.Token ?? string.Empty);
                using var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                PendingPasswordResetsResponse? payload = await response.Content.ReadFromJsonAsync<PendingPasswordResetsResponse>();
                if (payload?.Items == null || payload.Items.Count == 0)
                {
                    return;
                }

                foreach (var item in payload.Items)
                {
                    bool applied = false;
                    string note = string.Empty;
                    try
                    {
                        applied = _db.ResetPasswordByUsername(item.Username, item.TempPassword, mustChangePassword: true);
                        if (!applied)
                        {
                            note = "Bunday username topilmadi.";
                        }
                    }
                    catch (Exception ex)
                    {
                        applied = false;
                        note = ex.Message;
                    }

                    var ack = new
                    {
                        licenseKey = activation.LicenseKey,
                        deviceId = activation.DeviceId,
                        applied,
                        note
                    };
                    using var ackRequest = ActivationService.CreateAuthorizedJsonRequest(
                        HttpMethod.Post,
                        $"{baseUrl}/api/client/password-resets/{item.Id}/ack",
                        activation.Token ?? string.Empty,
                        ack);
                    using var ackRes = await Http.SendAsync(ackRequest);
                    _ = ackRes.IsSuccessStatusCode;
                }
            }
            catch
            {
                // Reset oqimi bajarilmasa keyingi ishga tushishda yana uriniladi.
            }
        }

        private async Task SendUsersSnapshotAsync(LocalActivationRecord activation, string appVersion)
        {
            try
            {
                var users = _db.GetAllUsers();
                var payload = new
                {
                    licenseKey = activation.LicenseKey,
                    deviceId = activation.DeviceId,
                    appVersion = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion,
                    userCount = users.Count,
                    usernames = users.Select(u => u.Username ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };

                using var request = ActivationService.CreateAuthorizedJsonRequest(
                    HttpMethod.Post,
                    $"{activation.ServerUrl.TrimEnd('/')}/api/client/telemetry/users",
                    activation.Token,
                    payload);
                using var response = await Http.SendAsync(request);
                _ = response.IsSuccessStatusCode;
            }
            catch
            {
                // Serverga chiqolmasa dastur ishini to'xtatmaymiz.
            }
        }

        private async Task<bool> UploadBackupAsync(LocalActivationRecord activation, string appVersion, bool force)
        {
            byte[]? encryptedBytes = null;
            try
            {
                string currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
                string markerId = Convert.ToHexString(SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{activation.LicenseKey}\0{activation.DeviceId}"))).ToLowerInvariant();
                string markerPath = Path.Combine(Database.GetAppDataRoot(), $"cloud_backup_month_{markerId}.marker");
                if (!force && File.Exists(markerPath))
                {
                    string savedMonth = File.ReadAllText(markerPath).Trim();
                    if (string.Equals(savedMonth, currentMonth, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                string backupPath = await Task.Run(() => _backupService.CreateBackup());
                byte[] backupBytes = await File.ReadAllBytesAsync(backupPath);
                try
                {
                    encryptedBytes = _backupCrypto.Encrypt(backupBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(backupBytes);
                }
                if (encryptedBytes.LongLength > 50L * 1024 * 1024)
                {
                    throw new InvalidOperationException("Shifrlangan backup serverning 50 MB limitidan oshdi.");
                }

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(activation.LicenseKey), "licenseKey");
                form.Add(new StringContent(activation.DeviceId), "deviceId");
                form.Add(new StringContent(string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion), "appVersion");
                form.Add(new StringContent("true"), "encrypted");

                var fileContent = new ByteArrayContent(encryptedBytes);
                form.Add(fileContent, "file", $"database_{currentMonth}.osonbackup");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{activation.ServerUrl.TrimEnd('/')}/api/client/backups/upload")
                {
                    Content = form
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", activation.Token ?? string.Empty);
                using var response = await Http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    File.WriteAllText(markerPath, currentMonth);
                    return true;
                }
                else
                {
                    LogHelper.WriteLog(
                        $"Bulutga zaxira nusxa yuborilmadi. Server javobi: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"Bulutga zaxira nusxa yuborilmadi: {ex.Message}");
                return false;
            }
            finally
            {
                if (encryptedBytes != null)
                {
                    CryptographicOperations.ZeroMemory(encryptedBytes);
                }
            }
        }
    }

    internal sealed class PendingPasswordResetsResponse
    {
        [JsonPropertyName("items")]
        public List<PendingPasswordResetItem> Items { get; set; } = new List<PendingPasswordResetItem>();
    }

    internal sealed class PendingPasswordResetItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("tempPassword")]
        public string TempPassword { get; set; } = string.Empty;
    }
}
