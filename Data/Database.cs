using Microsoft.Data.Sqlite;
using System.Configuration;
using System;
using System.IO;

namespace SantexnikaSRM.Data
{
    public static class Database
    {
        private const string AppDataFolderName = "OsontrackSRM";

        public static SqliteConnection GetConnection()
        {
            string connectionString = GetConnectionStringOrDefault();

            var builder = new SqliteConnectionStringBuilder(connectionString);
            string resolvedPath = ResolveDatabasePath(builder.DataSource);
            builder.DataSource = resolvedPath;
            builder.ForeignKeys = true;

            return new SqliteConnection(builder.ToString());
        }

        public static string GetResolvedDatabasePath()
        {
            string connectionString = GetConnectionStringOrDefault();
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return ResolveDatabasePath(builder.DataSource);
        }

        public static string GetAppDataRoot()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDataFolderName);
            Directory.CreateDirectory(root);
            return root;
        }

        private static string ResolveDatabasePath(string dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
            {
                dataSource = "database.db";
            }

            if (Path.IsPathRooted(dataSource))
            {
                string absoluteDir = Path.GetDirectoryName(dataSource) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(absoluteDir))
                {
                    Directory.CreateDirectory(absoluteDir);
                }

                return dataSource;
            }

            string appDataPath = Path.Combine(GetAppDataRoot(), dataSource);
            string appDataDir = Path.GetDirectoryName(appDataPath) ?? GetAppDataRoot();
            Directory.CreateDirectory(appDataDir);

            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dataSource);
            TryMigrateLegacyDatabase(legacyPath, appDataPath);
            return appDataPath;
        }

        private static string GetConnectionStringOrDefault()
        {
            try
            {
                ConnectionStringSettings? settings = ConfigurationManager.ConnectionStrings["DefaultConnection"];
                if (!string.IsNullOrWhiteSpace(settings?.ConnectionString))
                {
                    return settings.ConnectionString;
                }
            }
            catch
            {
                // Config o'qishda xatolik bo'lsa defaultga o'tamiz.
            }

            return "Data Source=database.db";
        }

        private static void TryMigrateLegacyDatabase(string legacyPath, string appDataPath)
        {
            try
            {
                if (File.Exists(appDataPath) || !File.Exists(legacyPath))
                {
                    return;
                }

                File.Copy(legacyPath, appDataPath, overwrite: false);
            }
            catch
            {
                // Migratsiya muvaffaqiyatsiz bo'lsa ulanish baribir appData path bilan davom etadi.
            }
        }
    }
}
