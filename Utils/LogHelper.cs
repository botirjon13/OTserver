using System;
using System.IO;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Utils
{
    public static class LogHelper
    {
        private static readonly string logFilePath = Path.Combine(Database.GetAppDataRoot(), "log.txt");

        public static void WriteLog(string message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    string logMessage = $"[{DateTime.Now}] {message}";
                    writer.WriteLine(logMessage);
                    Console.WriteLine(logMessage); // Konsolga chiqarish
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log yozishda xatolik: {ex.Message}");
            }
        }

        public static string GetLogFilePath()
        {
            return logFilePath;
        }

        public static void EnsureLogFileExists()
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    using (File.Create(logFilePath)) { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log faylini yaratishda xatolik: {ex.Message}");
            }
        }
    }
}
