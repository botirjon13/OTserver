using Microsoft.Data.Sqlite;
using System.Configuration;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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
                if (File.Exists(appDataPath))
                {
                    return;
                }

                var candidates = new List<string> { legacyPath };
                candidates.AddRange(GetAdditionalLegacyDatabaseCandidates(Path.GetFileName(appDataPath)));

                string? source = candidates
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path =>
                    {
                        try
                        {
                            if (!File.Exists(path))
                            {
                                return null;
                            }

                            var info = new FileInfo(path);
                            return info.Length > 0 ? info : null;
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(info => info != null)
                    .OrderByDescending(info => info!.LastWriteTimeUtc)
                    .Select(info => info!.FullName)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(source))
                {
                    return;
                }

                File.Copy(source, appDataPath, overwrite: false);
            }
            catch
            {
                // Migratsiya muvaffaqiyatsiz bo'lsa ulanish baribir appData path bilan davom etadi.
            }
        }

        private static IEnumerable<string> GetAdditionalLegacyDatabaseCandidates(string fileName)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                yield return Path.Combine(localAppData, "Programs", AppDataFolderName, fileName);
                yield return Path.Combine(localAppData, "Programs", AppDataFolderName, "current", fileName);
                yield return Path.Combine(localAppData, AppDataFolderName, "current", fileName);
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                yield return Path.Combine(appData, AppDataFolderName, fileName);
            }
        }
    }
}
