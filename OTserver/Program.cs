using System;
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

                if (!EnsureActivation())
                {
                    return;
                }

                CurrencyHelper.CheckAndUpdateRate(db).GetAwaiter().GetResult();
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

            Application.Run(new LoginForm());
        }

        private static bool EnsureActivation()
        {
            ActivationService activationService = new ActivationService();
            while (!activationService.TryGetValidLocalActivation(out _, out string message))
            {
                using ActivationForm activationForm = new ActivationForm(activationService, message);
                if (activationForm.ShowDialog() != DialogResult.OK)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
