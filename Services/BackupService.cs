using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Services
{
    public class BackupService
    {
        public string CreateBackup()
        {
            string dbPath = GetDatabasePath();
            string backupDirectory = GetBackupDirectory();
            Directory.CreateDirectory(backupDirectory);

            string backupFileName = $"database_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            string backupFilePath = Path.Combine(backupDirectory, backupFileName);

            using var source = new SqliteConnection(BuildConnectionStringByPath(dbPath));
            using var target = new SqliteConnection(BuildConnectionStringByPath(backupFilePath));
            source.Open();
            target.Open();
            source.BackupDatabase(target);
            TryBackupProductImages(backupFilePath);
            return backupFilePath;
        }

        public void EnsureDailyBackup()
        {
            string backupDirectory = GetBackupDirectory();
            Directory.CreateDirectory(backupDirectory);

            string todayPrefix = $"database_{DateTime.Now:yyyyMMdd}_";
            bool existsToday = Directory
                .GetFiles(backupDirectory, "database_*.db", SearchOption.TopDirectoryOnly)
                .Any(file => Path.GetFileName(file).StartsWith(todayPrefix, StringComparison.OrdinalIgnoreCase));

            if (!existsToday)
            {
                _ = CreateBackup();
            }
        }

        public List<string> ListBackups()
        {
            string backupDirectory = GetBackupDirectory();
            if (!Directory.Exists(backupDirectory))
            {
                return new List<string>();
            }

            return Directory
                .GetFiles(backupDirectory, "database_*.db", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetCreationTimeUtc)
                .ToList();
        }

        public void RestoreBackup(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            {
                throw new Exception("Tanlangan backup fayli topilmadi.");
            }

            string dbPath = GetDatabasePath();
            using var source = new SqliteConnection(BuildConnectionStringByPath(backupFilePath));
            using var target = new SqliteConnection(BuildConnectionStringByPath(dbPath));
            source.Open();
            target.Open();
            source.BackupDatabase(target);
            TryRestoreProductImages(backupFilePath);
        }

        private static string GetBackupDirectory()
        {
            string? backupPath = ConfigurationManager.AppSettings["BackupPath"];
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                backupPath = "Backup";
            }

            if (Path.IsPathRooted(backupPath))
            {
                return backupPath;
            }

            return Path.Combine(Database.GetAppDataRoot(), backupPath);
        }

        private static string GetDatabasePath()
        {
            return Database.GetResolvedDatabasePath();
        }

        private static string BuildConnectionStringByPath(string dbPath)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true
            };

            return builder.ToString();
        }

        private static void TryBackupProductImages(string backupFilePath)
        {
            try
            {
                BackupProductImages(backupFilePath);
            }
            catch
            {
                // DB backup muhim; rasm nusxasida muammo bo'lsa jarayonni to'xtatmaymiz.
            }
        }

        private static void TryRestoreProductImages(string backupFilePath)
        {
            try
            {
                RestoreProductImages(backupFilePath);
            }
            catch
            {
                // DB restore muvaffaqiyatli bo'lsa ham rasm nusxasi bo'lmasligi mumkin.
            }
        }

        private static void BackupProductImages(string backupFilePath)
        {
            string sourceImagesRoot = Path.Combine(Database.GetAppDataRoot(), "ProductImages");
            if (!Directory.Exists(sourceImagesRoot))
            {
                return;
            }

            string sidecarRoot = GetBackupImagesSidecarRoot(backupFilePath);
            string targetImagesRoot = Path.Combine(sidecarRoot, "ProductImages");

            if (Directory.Exists(targetImagesRoot))
            {
                Directory.Delete(targetImagesRoot, recursive: true);
            }

            Directory.CreateDirectory(sidecarRoot);
            CopyDirectory(sourceImagesRoot, targetImagesRoot);
        }

        private static void RestoreProductImages(string backupFilePath)
        {
            string sidecarRoot = GetBackupImagesSidecarRoot(backupFilePath);
            string sourceImagesRoot = Path.Combine(sidecarRoot, "ProductImages");
            if (!Directory.Exists(sourceImagesRoot))
            {
                return;
            }

            string targetImagesRoot = Path.Combine(Database.GetAppDataRoot(), "ProductImages");
            if (Directory.Exists(targetImagesRoot))
            {
                Directory.Delete(targetImagesRoot, recursive: true);
            }

            CopyDirectory(sourceImagesRoot, targetImagesRoot);
        }

        private static string GetBackupImagesSidecarRoot(string backupFilePath)
        {
            string backupDir = Path.GetDirectoryName(backupFilePath) ?? GetBackupDirectory();
            string backupName = Path.GetFileNameWithoutExtension(backupFilePath);
            return Path.Combine(backupDir, backupName + "_assets");
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, overwrite: true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
            }
        }
    }
}
