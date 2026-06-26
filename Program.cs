using System;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Forms;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
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
    }
}
