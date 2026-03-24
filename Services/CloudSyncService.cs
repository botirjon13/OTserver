using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Services
{
    public sealed class CloudSyncService
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly DatabaseHelper _db = new DatabaseHelper();
        private readonly BackupService _backupService = new BackupService();

        public async Task RunAsync(LocalActivationRecord activation, string appVersion)
        {
            if (activation == null || string.IsNullOrWhiteSpace(activation.ServerUrl))
            {
                return;
            }

            await ApplyPendingPasswordResetsAsync(activation);
            await SendUsersSnapshotAsync(activation, appVersion);
            await UploadMonthlyBackupAsync(activation, appVersion);
        }

        public Task ApplyPendingPasswordResetsNowAsync(LocalActivationRecord activation)
        {
            if (activation == null || string.IsNullOrWhiteSpace(activation.ServerUrl))
            {
                return Task.CompletedTask;
            }

            return ApplyPendingPasswordResetsAsync(activation);
        }

        private async Task ApplyPendingPasswordResetsAsync(LocalActivationRecord activation)
        {
            try
            {
                string baseUrl = activation.ServerUrl.TrimEnd('/');
                string url = $"{baseUrl}/api/client/password-resets/pending?licenseKey={Uri.EscapeDataString(activation.LicenseKey)}&deviceId={Uri.EscapeDataString(activation.DeviceId)}";
                using var response = await Http.GetAsync(url);
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
                    using var ackRes = await Http.PostAsJsonAsync($"{baseUrl}/api/client/password-resets/{item.Id}/ack", ack);
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

                using var response = await Http.PostAsJsonAsync($"{activation.ServerUrl.TrimEnd('/')}/api/client/telemetry/users", payload);
                _ = response.IsSuccessStatusCode;
            }
            catch
            {
                // Serverga chiqolmasa dastur ishini to'xtatmaymiz.
            }
        }

        private async Task UploadMonthlyBackupAsync(LocalActivationRecord activation, string appVersion)
        {
            try
            {
                string currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
                string markerPath = Path.Combine(Database.GetAppDataRoot(), "cloud_backup_month.marker");
                if (File.Exists(markerPath))
                {
                    string savedMonth = File.ReadAllText(markerPath).Trim();
                    if (string.Equals(savedMonth, currentMonth, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                string backupPath = _backupService.CreateBackup();
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(activation.LicenseKey), "licenseKey");
                form.Add(new StringContent(activation.DeviceId), "deviceId");
                form.Add(new StringContent(string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion), "appVersion");

                await using var fs = File.OpenRead(backupPath);
                var fileContent = new StreamContent(fs);
                form.Add(fileContent, "file", Path.GetFileName(backupPath));

                using var response = await Http.PostAsync($"{activation.ServerUrl.TrimEnd('/')}/api/client/backups/upload", form);
                if (response.IsSuccessStatusCode)
                {
                    File.WriteAllText(markerPath, currentMonth);
                }
            }
            catch
            {
                // Upload bo'lmasa keyingi ishga tushishda yana uriniladi.
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
