using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Forms;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;
using Velopack;

namespace SantexnikaSRM
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            VelopackApp.Build().Run();
            ApplicationConfiguration.Initialize();
            DatabaseHelper db = new DatabaseHelper();

            LogHelper.EnsureLogFileExists();

            try
            {
                LogHelper.WriteLog("Dastur ishga tushmoqda...");

                DbInitializer.Initialize();
                LogHelper.WriteLog("Database initialized successfully.");

                db.CreateUsersTable();
                LogHelper.WriteLog("Users table created successfully.");

                db.CreateDefaultUser();
                LogHelper.WriteLog("Default admin created successfully.");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await CurrencyHelper.CheckAndUpdateRate(db);
                        LogHelper.WriteLog("Currency rate background check completed.");
                    }
                    catch (Exception rateEx)
                    {
                        LogHelper.WriteLog($"Currency rate background check failed: {rateEx.Message}");
                    }
                });

                var backupService = new BackupService();
                backupService.EnsureDailyBackup();
                LogHelper.WriteLog("Daily backup check completed.");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"Error: {ex.Message}");
                MessageBox.Show($"Bazaga bog'lanishda xato: {ex.Message}", 
                                "Xatolik", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                return;
            }

            var activationService = new ActivationService();
            if (!activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out string activationMessage) || activation == null)
            {
                using var activationForm = new ActivationForm(activationService, activationMessage);
                if (activationForm.ShowDialog() != DialogResult.OK || activationForm.Activation == null)
                {
                    MessageBox.Show("Aktivatsiya qilinmagani uchun dastur yopildi.", "Aktivatsiya talab qilinadi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                activation = activationForm.Activation;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await activationService.SendHeartbeatAsync(activation, Application.ProductVersion);
                    LogHelper.WriteLog("Activation heartbeat sent.");
                }
                catch (Exception hbEx)
                {
                    LogHelper.WriteLog($"Activation heartbeat failed: {hbEx.Message}");
                }
            });

            try
            {
                var resetSync = new CloudSyncService();
                resetSync.ApplyPendingPasswordResetsNowAsync(activation).GetAwaiter().GetResult();
                LogHelper.WriteLog("Pending password reset sync completed before login.");
            }
            catch (Exception preResetEx)
            {
                LogHelper.WriteLog($"Pre-login password reset sync failed: {preResetEx.Message}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var cloudSync = new CloudSyncService();
                    await cloudSync.RunAsync(activation, Application.ProductVersion);
                    LogHelper.WriteLog("Cloud sync completed.");
                }
                catch (Exception syncEx)
                {
                    LogHelper.WriteLog($"Cloud sync failed: {syncEx.Message}");
                }
            });

            Application.Run(new LoginForm());
        }
    }
}
