using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Win32;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Services
{
    public sealed class ActivationService
    {
        private const string ActivationFileName = "license_activation.dat";
        private const string FallbackDeviceIdFileName = "device_id.txt";
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        public string GetDefaultServerUrl()
        {
            string? configured = ConfigurationManager.AppSettings["ActivationServerUrl"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                return "https://otserver-production.up.railway.app";
            }

            return configured.Trim().TrimEnd('/');
        }

        public string GetDeviceId()
        {
            string machineGuid = ReadMachineGuid();
            if (!string.IsNullOrWhiteSpace(machineGuid))
            {
                return $"WIN-{machineGuid}";
            }

            string file = Path.Combine(Database.GetAppDataRoot(), FallbackDeviceIdFileName);
            if (File.Exists(file))
            {
                string existing = File.ReadAllText(file).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return existing;
                }
            }

            string generated = $"WIN-{Guid.NewGuid():N}".ToUpperInvariant();
            File.WriteAllText(file, generated);
            return generated;
        }

        public bool TryGetValidLocalActivation(out LocalActivationRecord? activation, out string message)
        {
            activation = null;
            string file = Path.Combine(Database.GetAppDataRoot(), ActivationFileName);
            if (!File.Exists(file))
            {
                message = "Aktivatsiya topilmadi. Iltimos online aktivatsiya qiling.";
                return false;
            }

            try
            {
                byte[] encrypted = File.ReadAllBytes(file);
                byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                LocalActivationRecord? loaded = JsonSerializer.Deserialize<LocalActivationRecord>(plain);
                if (loaded == null)
                {
                    message = "Aktivatsiya fayli noto'g'ri.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(loaded.LicenseKey) ||
                    string.IsNullOrWhiteSpace(loaded.DeviceId) ||
                    string.IsNullOrWhiteSpace(loaded.Token) ||
                    string.IsNullOrWhiteSpace(loaded.ServerUrl))
                {
                    message = "Aktivatsiya ma'lumoti to'liq emas.";
                    return false;
                }

                string currentDevice = GetDeviceId();
                if (!string.Equals(currentDevice, loaded.DeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Aktivatsiya boshqa qurilma uchun berilgan.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(loaded.ExpiresAtUtc))
                {
                    if (DateTime.TryParse(loaded.ExpiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime exp))
                    {
                        if (DateTime.UtcNow.Date > exp.Date)
                        {
                            message = "License muddati tugagan.";
                            return false;
                        }
                    }
                }

                activation = loaded;
                message = "OK";
                return true;
            }
            catch
            {
                message = "Aktivatsiya faylini o'qishda xatolik.";
                return false;
            }
        }

        public async Task<ActivationOnlineResult> ActivateAsync(string serverUrl, string licenseKey, string appVersion)
        {
            serverUrl = (serverUrl ?? string.Empty).Trim().TrimEnd('/');
            licenseKey = (licenseKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return ActivationOnlineResult.Fail("Server URL bo'sh.");
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return ActivationOnlineResult.Fail("License key bo'sh.");
            }
            licenseKey = licenseKey.ToUpperInvariant();

            string deviceId = GetDeviceId();
            var payload = new
            {
                licenseKey,
                deviceId,
                deviceName = Environment.MachineName,
                appVersion = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion
            };

            try
            {
                string activateUrl = $"{serverUrl}/api/activate";
                using var response = await Http.PostAsJsonAsync(activateUrl, payload);
                string raw = await response.Content.ReadAsStringAsync();
                ActivationApiResponse? body = null;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<ActivationApiResponse>(raw, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch
                    {
                        // Server JSON bermasa ham foydali xato qaytaramiz.
                    }
                }

                if (!response.IsSuccessStatusCode || body == null || !body.Ok)
                {
                    string err;
                    if (body?.Error is { Length: > 0 })
                    {
                        err = body.Error;
                    }
                    else if (string.IsNullOrWhiteSpace(raw))
                    {
                        err = $"Server bo'sh javob qaytardi: {(int)response.StatusCode}. URL: {activateUrl}";
                    }
                    else
                    {
                        string preview = raw.Length > 160 ? raw[..160] + "..." : raw;
                        err = $"Server JSON emas ({(int)response.StatusCode}). URL: {activateUrl}. Javob: {preview}";
                    }

                    return ActivationOnlineResult.Fail(err);
                }

                int? firstLoginId = body.FirstLogin?.Id;
                string firstLoginUsername = body.FirstLogin?.Username?.Trim() ?? string.Empty;
                string firstLoginPassword = body.FirstLogin?.Password ?? string.Empty;
                bool firstLoginApplied = false;
                string firstLoginNote = string.Empty;
                if (firstLoginId.HasValue &&
                    !string.IsNullOrWhiteSpace(firstLoginUsername) &&
                    !string.IsNullOrWhiteSpace(firstLoginPassword))
                {
                    try
                    {
                        var db = new DatabaseHelper();
                        firstLoginApplied = db.ResetPasswordByUsername(firstLoginUsername, firstLoginPassword, mustChangePassword: true);
                        if (!firstLoginApplied)
                        {
                            db.AddUser(firstLoginUsername, firstLoginPassword, DatabaseHelper.RoleAdmin);
                            firstLoginApplied = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        firstLoginApplied = false;
                        firstLoginNote = ex.Message;
                    }
                }

                var record = new LocalActivationRecord
                {
                    LicenseKey = licenseKey,
                    DeviceId = deviceId,
                    DeviceName = Environment.MachineName,
                    ServerUrl = serverUrl,
                    Token = body.Token ?? string.Empty,
                    ActivatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    ExpiresAtUtc = body.ExpiresAt
                };

                SaveLocalActivation(record);

                if (firstLoginId.HasValue)
                {
                    try
                    {
                        var ackPayload = new
                        {
                            licenseKey,
                            deviceId,
                            applied = firstLoginApplied,
                            note = firstLoginNote
                        };
                        _ = await Http.PostAsJsonAsync($"{serverUrl}/api/client/first-login/{firstLoginId.Value}/ack", ackPayload);
                    }
                    catch
                    {
                        // Ack ketmasa ham aktivatsiya muvaffaqiyatli hisoblanadi.
                    }
                }

                return ActivationOnlineResult.Success(record, firstLoginUsername, firstLoginPassword, body.SupportContact ?? string.Empty);
            }
            catch (Exception ex)
            {
                return ActivationOnlineResult.Fail($"Ulanishda xatolik: {ex.Message}");
            }
        }

        public async Task<string> GetSupportContactAsync(string serverUrl)
        {
            serverUrl = (serverUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return GetLocalSupportFallback();
            }

            try
            {
                using var response = await Http.GetAsync($"{serverUrl}/api/contact/info");
                if (!response.IsSuccessStatusCode)
                {
                    return GetLocalSupportFallback();
                }

                SupportContactResponse? body = await response.Content.ReadFromJsonAsync<SupportContactResponse>();
                if (body == null || !body.Ok || string.IsNullOrWhiteSpace(body.Contact))
                {
                    return GetLocalSupportFallback();
                }

                return body.Contact.Trim();
            }
            catch
            {
                return GetLocalSupportFallback();
            }
        }

        private static string GetLocalSupportFallback()
        {
            string? configured = ConfigurationManager.AppSettings["SupportContactFallback"];
            return string.IsNullOrWhiteSpace(configured)
                ? "Aloqa: +998 90 000 00 00"
                : configured.Trim();
        }

        public async Task SendHeartbeatAsync(LocalActivationRecord activation, string appVersion)
        {
            if (activation == null || string.IsNullOrWhiteSpace(activation.ServerUrl))
            {
                return;
            }

            var payload = new
            {
                licenseKey = activation.LicenseKey,
                deviceId = activation.DeviceId,
                appVersion = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion
            };

            try
            {
                using var response = await Http.PostAsJsonAsync($"{activation.ServerUrl.TrimEnd('/')}/api/heartbeat", payload);
                _ = response.IsSuccessStatusCode;
            }
            catch
            {
                // Offline bo'lsa yoki serverga chiqmasa, dastur ishini to'xtatmaymiz.
            }
        }

        private static void SaveLocalActivation(LocalActivationRecord record)
        {
            string file = Path.Combine(Database.GetAppDataRoot(), ActivationFileName);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(record);
            byte[] encrypted = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(file, encrypted);
        }

        private static string ReadMachineGuid()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                string? guid = key?.GetValue("MachineGuid")?.ToString();
                return string.IsNullOrWhiteSpace(guid) ? string.Empty : guid.Trim().ToUpperInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class ActivationApiResponse
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; set; }

            [JsonPropertyName("token")]
            public string? Token { get; set; }

            [JsonPropertyName("expires_at")]
            public string? ExpiresAt { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("support_contact")]
            public string? SupportContact { get; set; }

            [JsonPropertyName("first_login")]
            public FirstLoginPayload? FirstLogin { get; set; }
        }

        private sealed class FirstLoginPayload
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("username")]
            public string? Username { get; set; }

            [JsonPropertyName("password")]
            public string? Password { get; set; }
        }

        private sealed class SupportContactResponse
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; set; }

            [JsonPropertyName("contact")]
            public string? Contact { get; set; }
        }
    }

    public sealed class LocalActivationRecord
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string ActivatedAtUtc { get; set; } = string.Empty;
        public string? ExpiresAtUtc { get; set; }
    }

    public sealed class ActivationOnlineResult
    {
        private ActivationOnlineResult(bool ok, string? error, LocalActivationRecord? activation)
        {
            Ok = ok;
            Error = error;
            Activation = activation;
            FirstLoginUsername = string.Empty;
            FirstLoginPassword = string.Empty;
            SupportContact = string.Empty;
        }

        private ActivationOnlineResult(bool ok, string? error, LocalActivationRecord? activation, string firstLoginUsername, string firstLoginPassword, string supportContact)
        {
            Ok = ok;
            Error = error;
            Activation = activation;
            FirstLoginUsername = firstLoginUsername;
            FirstLoginPassword = firstLoginPassword;
            SupportContact = supportContact;
        }

        public bool Ok { get; }
        public string? Error { get; }
        public LocalActivationRecord? Activation { get; }
        public string FirstLoginUsername { get; }
        public string FirstLoginPassword { get; }
        public string SupportContact { get; }

        public static ActivationOnlineResult Success(LocalActivationRecord activation, string firstLoginUsername, string firstLoginPassword, string supportContact)
            => new ActivationOnlineResult(true, null, activation, firstLoginUsername, firstLoginPassword, supportContact);
        public static ActivationOnlineResult Fail(string error) => new ActivationOnlineResult(false, error, null);
    }
}
