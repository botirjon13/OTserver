using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Services
{
    public class BackupService
    {
        private const string DatabaseEntryName = "database.db";
        private const string ProductImagesEntryName = "ProductImages/";

        public string CreateBackup()
        {
            string dbPath = GetDatabasePath();
            string backupDirectory = GetBackupDirectory();
            Directory.CreateDirectory(backupDirectory);

            string backupFileName = $"database_{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip";
            string backupFilePath = Path.Combine(backupDirectory, backupFileName);
            string temporaryDirectory = Path.Combine(backupDirectory, $".backup_{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                string databaseCopyPath = Path.Combine(temporaryDirectory, DatabaseEntryName);
                CopyDatabase(dbPath, databaseCopyPath);

                string archivePath = Path.Combine(temporaryDirectory, "backup.zip");
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(databaseCopyPath, DatabaseEntryName, CompressionLevel.Optimal);
                    archive.CreateEntry(ProductImagesEntryName);

                    string imagesRoot = GetProductImagesRoot();
                    if (Directory.Exists(imagesRoot))
                    {
                        foreach (string filePath in Directory.GetFiles(imagesRoot, "*", SearchOption.AllDirectories))
                        {
                            string relativePath = Path.GetRelativePath(imagesRoot, filePath)
                                .Replace(Path.DirectorySeparatorChar, '/')
                                .Replace(Path.AltDirectorySeparatorChar, '/');
                            archive.CreateEntryFromFile(
                                filePath,
                                ProductImagesEntryName + relativePath,
                                CompressionLevel.Optimal);
                        }
                    }
                }

                File.Move(archivePath, backupFilePath);
                return backupFilePath;
            }
            finally
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }

        public void EnsureDailyBackup()
        {
            string backupDirectory = GetBackupDirectory();
            Directory.CreateDirectory(backupDirectory);

            string todayPrefix = $"database_{DateTime.Now:yyyyMMdd}_";
            bool existsToday = Directory
                .GetFiles(backupDirectory, "database_*", SearchOption.TopDirectoryOnly)
                .Any(file =>
                    string.Equals(Path.GetExtension(file), ".zip", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(file).StartsWith(todayPrefix, StringComparison.OrdinalIgnoreCase));

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
                .GetFiles(backupDirectory, "database_*", SearchOption.TopDirectoryOnly)
                .Where(path =>
                    string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(path), ".sqlite", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(path), ".sqlite3", StringComparison.OrdinalIgnoreCase))
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
            string temporaryDirectory = Path.Combine(Database.GetAppDataRoot(), $".restore_{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);
            bool preserveTemporaryDirectory = false;

            try
            {
                string sourceDatabasePath = Path.Combine(temporaryDirectory, DatabaseEntryName);
                bool hasProductImages;
                string? stagedImagesRoot;
                string extension = Path.GetExtension(backupFilePath);

                if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    (hasProductImages, stagedImagesRoot) = ExtractBackupArchive(backupFilePath, temporaryDirectory);
                }
                else if (IsSqliteBackupExtension(extension))
                {
                    sourceDatabasePath = backupFilePath;
                    string legacyImagesRoot = Path.Combine(
                        GetLegacyBackupImagesRoot(backupFilePath),
                        "ProductImages");
                    hasProductImages = Directory.Exists(legacyImagesRoot);
                    stagedImagesRoot = hasProductImages
                        ? CopyDirectoryToTemporaryLocation(legacyImagesRoot, temporaryDirectory)
                        : null;
                }
                else
                {
                    throw new InvalidDataException("Backup fayl turi qo'llab-quvvatlanmaydi.");
                }

                ValidateDatabase(sourceDatabasePath);
                string rollbackDatabasePath = Path.Combine(temporaryDirectory, "previous.db");
                CopyDatabase(dbPath, rollbackDatabasePath);
                (string? previousImagesRoot, bool hadPreviousImages) imageSwap;
                try
                {
                    imageSwap = hasProductImages
                        ? SwapProductImages(stagedImagesRoot, temporaryDirectory)
                        : (null, false);
                }
                catch (Exception swapException)
                {
                    string previousImagesRoot = Path.Combine(temporaryDirectory, "PreviousProductImages");
                    if (Directory.Exists(previousImagesRoot))
                    {
                        preserveTemporaryDirectory = true;
                        throw new IOException(
                            $"Rasmlarni almashtirishda xatolik yuz berdi. Eski rasmlar vaqtinchalik papkada saqlandi: {temporaryDirectory}",
                            swapException);
                    }

                    throw;
                }

                try
                {
                    RestoreDatabase(sourceDatabasePath, dbPath);
                }
                catch (Exception restoreException)
                {
                    var rollbackExceptions = new List<Exception>();
                    try
                    {
                        RestoreDatabase(rollbackDatabasePath, dbPath);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackExceptions.Add(rollbackException);
                    }

                    if (hasProductImages)
                    {
                        try
                        {
                            RestorePreviousProductImages(
                                imageSwap.previousImagesRoot,
                                imageSwap.hadPreviousImages);
                        }
                        catch (Exception rollbackException)
                        {
                            rollbackExceptions.Add(rollbackException);
                        }
                    }

                    if (rollbackExceptions.Count > 0)
                    {
                        preserveTemporaryDirectory = true;
                        rollbackExceptions.Insert(0, restoreException);
                        throw new AggregateException(
                            $"Backup tiklanmadi va oldingi ma'lumotlarni to'liq qaytarishda xatolik yuz berdi. Qutqaruv fayllari saqlandi: {temporaryDirectory}",
                            rollbackExceptions);
                    }

                    throw;
                }

                if (!string.IsNullOrWhiteSpace(imageSwap.previousImagesRoot))
                {
                    Directory.Delete(imageSwap.previousImagesRoot, recursive: true);
                }
            }
            finally
            {
                if (!preserveTemporaryDirectory)
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        private static (bool HasProductImages, string? StagedImagesRoot) ExtractBackupArchive(
            string backupFilePath,
            string temporaryDirectory)
        {
            string databasePath = Path.Combine(temporaryDirectory, DatabaseEntryName);
            string stagedImagesRoot = Path.Combine(temporaryDirectory, "ProductImages");
            bool hasDatabase = false;
            bool hasProductImages = false;
            long expandedSize = 0;
            const long maximumExpandedSize = 1024L * 1024 * 1024;
            var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var archive = ZipFile.OpenRead(backupFilePath))
            {
                if (archive.Entries.Count > 20000)
                {
                    throw new InvalidDataException("Backup ichida fayllar soni me'yordan oshib ketdi.");
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.Length > maximumExpandedSize - expandedSize)
                    {
                        throw new InvalidDataException("Backup ochilganda hajmi 1 GB dan oshadi.");
                    }

                    expandedSize += entry.Length;
                    string normalizedName = entry.FullName.Replace('\\', '/');
                    if (string.Equals(normalizedName, DatabaseEntryName, StringComparison.Ordinal))
                    {
                        if (hasDatabase || string.IsNullOrEmpty(entry.Name))
                        {
                            throw new InvalidDataException("Backup ichida ma'lumotlar bazasi noto'g'ri ko'rsatilgan.");
                        }

                        using Stream databaseSource = entry.Open();
                        using FileStream databaseTarget = new FileStream(databasePath, FileMode.CreateNew, FileAccess.Write);
                        databaseSource.CopyTo(databaseTarget);
                        hasDatabase = true;
                        continue;
                    }

                    if (string.Equals(normalizedName, ProductImagesEntryName.TrimEnd('/'), StringComparison.Ordinal) ||
                        string.Equals(normalizedName, ProductImagesEntryName, StringComparison.Ordinal))
                    {
                        hasProductImages = true;
                        Directory.CreateDirectory(stagedImagesRoot);
                        continue;
                    }

                    if (!normalizedName.StartsWith(ProductImagesEntryName, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Backup ichida kutilmagan fayl bor: {entry.FullName}");
                    }

                    string relativePath = normalizedName[ProductImagesEntryName.Length..];
                    if (!extractedPaths.Add(relativePath))
                    {
                        throw new InvalidDataException($"Backup ichida takrorlangan rasm yo'li bor: {relativePath}");
                    }

                    string targetPath = GetSafeImageEntryPath(stagedImagesRoot, relativePath);
                    hasProductImages = true;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(targetPath);
                        continue;
                    }

                    string? targetDirectory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    using Stream source = entry.Open();
                    using FileStream target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write);
                    source.CopyTo(target);
                }
            }

            if (!hasDatabase)
            {
                throw new InvalidDataException("Backup ichida ma'lumotlar bazasi topilmadi.");
            }

            if (!hasProductImages)
            {
                throw new InvalidDataException("Backup ichida mahsulot rasmlari bo'limi topilmadi.");
            }

            return (true, stagedImagesRoot);
        }

        private static string GetSafeImageEntryPath(string imagesRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Split('/').Any(part => part is "." or ".." || part.Contains(':')))
            {
                throw new InvalidDataException("Backup ichida noto'g'ri rasm yo'li bor.");
            }

            string rootPath = Path.GetFullPath(imagesRoot) + Path.DirectorySeparatorChar;
            string targetPath = Path.GetFullPath(Path.Combine(imagesRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Backup ichidagi rasm yo'li ruxsat etilgan papkadan tashqariga chiqadi.");
            }

            return targetPath;
        }

        private static string CopyDirectoryToTemporaryLocation(string sourceDirectory, string temporaryDirectory)
        {
            string targetDirectory = Path.Combine(temporaryDirectory, "ProductImages");
            CopyDirectory(sourceDirectory, targetDirectory);
            return targetDirectory;
        }

        private static (string? PreviousImagesRoot, bool HadPreviousImages) SwapProductImages(
            string? stagedImagesRoot,
            string temporaryDirectory)
        {
            string currentImagesRoot = GetProductImagesRoot();
            string previousImagesRoot = Path.Combine(temporaryDirectory, "PreviousProductImages");
            bool movedCurrentImages = false;

            if (Directory.Exists(currentImagesRoot))
            {
                Directory.Move(currentImagesRoot, previousImagesRoot);
                movedCurrentImages = true;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(stagedImagesRoot) && Directory.Exists(stagedImagesRoot))
                {
                    Directory.Move(stagedImagesRoot, currentImagesRoot);
                }
                else
                {
                    Directory.CreateDirectory(currentImagesRoot);
                }
            }
            catch
            {
                if (movedCurrentImages && !Directory.Exists(currentImagesRoot))
                {
                    Directory.Move(previousImagesRoot, currentImagesRoot);
                }

                throw;
            }

            return (movedCurrentImages ? previousImagesRoot : null, movedCurrentImages);
        }

        private static void RestorePreviousProductImages(string? previousImagesRoot, bool hadPreviousImages)
        {
            string currentImagesRoot = GetProductImagesRoot();
            if (Directory.Exists(currentImagesRoot))
            {
                Directory.Delete(currentImagesRoot, recursive: true);
            }

            if (hadPreviousImages && !string.IsNullOrWhiteSpace(previousImagesRoot))
            {
                Directory.Move(previousImagesRoot, currentImagesRoot);
            }
        }

        private static void ValidateDatabase(string databasePath)
        {
            if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
            {
                throw new InvalidDataException("Backup ichidagi ma'lumotlar bazasi bo'sh yoki topilmadi.");
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                ForeignKeys = true,
                Pooling = false
            };

            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            object? result = command.ExecuteScalar();
            if (!string.Equals(Convert.ToString(result), "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Backup ichidagi ma'lumotlar bazasi tekshiruvdan o'tmadi.");
            }
        }

        private static void CopyDatabase(string sourcePath, string targetPath)
        {
            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                Pooling = false
            };
            var targetBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = targetPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                Pooling = false
            };

            using var source = new SqliteConnection(sourceBuilder.ToString());
            using var target = new SqliteConnection(targetBuilder.ToString());
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        private static void RestoreDatabase(string sourcePath, string targetPath)
        {
            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadOnly,
                ForeignKeys = true,
                Pooling = false
            };
            var targetBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = targetPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                Pooling = false
            };

            using var source = new SqliteConnection(sourceBuilder.ToString());
            using var target = new SqliteConnection(targetBuilder.ToString());
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        private static bool IsSqliteBackupExtension(string extension)
        {
            return string.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".sqlite", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".sqlite3", StringComparison.OrdinalIgnoreCase);
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

        private static string GetProductImagesRoot()
        {
            return Path.Combine(Database.GetAppDataRoot(), "ProductImages");
        }

        private static string GetLegacyBackupImagesRoot(string backupFilePath)
        {
            string backupDirectory = Path.GetDirectoryName(backupFilePath) ?? GetBackupDirectory();
            string backupName = Path.GetFileNameWithoutExtension(backupFilePath);
            return Path.Combine(backupDirectory, backupName + "_assets");
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, overwrite: false);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
            }
        }
    }
}
