using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Security.Cryptography;
using SantexnikaSRM.Models;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Data
{
    public class DatabaseHelper
    {
        private const string DefaultAdminUsername = "admin";
        private const string DefaultSellerUsername = "seller";
        private const string AdminUsernameKey = "AdminUsername";
        private const string SellerUsernameKey = "SellerUsername";
        private const string AdminInitialPasswordKey = "AdminInitialPassword";
        private const string SellerInitialPasswordKey = "SellerInitialPassword";
        public const string RoleAdmin = "Admin";
        public const string RoleSeller = "Seller";
        private const int PasswordIterations = 120000;

        /// <summary>
        /// Bazada foydalanuvchi bo'lmasa, admin yaratish
        /// </summary>
        public void CreateDefaultUser()
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                string configuredUsername = GetSetting(AdminUsernameKey, DefaultAdminUsername);
                string configuredSellerUsername = GetSetting(SellerUsernameKey, DefaultSellerUsername);
                string configuredAdminInitialPassword = GetOptionalSetting(AdminInitialPasswordKey);
                string configuredSellerInitialPassword = GetOptionalSetting(SellerInitialPasswordKey);

                string? adminTempPassword = EnsureRoleUser(connection, configuredUsername, RoleAdmin, configuredAdminInitialPassword);
                string? sellerTempPassword = EnsureRoleUser(connection, configuredSellerUsername, RoleSeller, configuredSellerInitialPassword);

                if (!string.IsNullOrWhiteSpace(adminTempPassword))
                {
                    LogHelper.WriteLog($"Admin akkaunti yaratildi. Login: {configuredUsername}; boshlang'ich parol appSettings'dan olingan.");
                }

                if (!string.IsNullOrWhiteSpace(sellerTempPassword))
                {
                    LogHelper.WriteLog($"Seller akkaunti yaratildi. Login: {configuredSellerUsername}; boshlang'ich parol appSettings'dan olingan.");
                }
            }
        }

        /// <summary>
        /// Foydalanuvchilar jadvalini yaratish
        /// </summary>
        public void CreateUsersTable()
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                const string usersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    Password TEXT NULL,
                    PasswordHash TEXT NULL,
                    PasswordSalt TEXT NULL,
                    MustChangePassword INTEGER NOT NULL DEFAULT 0,
                    Role TEXT NOT NULL DEFAULT 'Seller' CHECK(Role IN ('Admin', 'Seller'))
                );";

                var cmd = connection.CreateCommand();
                cmd.CommandText = usersTable;
                cmd.ExecuteNonQuery();

                // Legacy baza uchun migratsiya.
                TryAddColumn(connection, "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'Seller'");
                TryAddColumn(connection, "ALTER TABLE Users ADD COLUMN Password TEXT NULL");
                TryAddColumn(connection, "ALTER TABLE Users ADD COLUMN PasswordHash TEXT NULL");
                TryAddColumn(connection, "ALTER TABLE Users ADD COLUMN PasswordSalt TEXT NULL");
                TryAddColumn(connection, "ALTER TABLE Users ADD COLUMN MustChangePassword INTEGER NOT NULL DEFAULT 0");

                MigrateLegacyPasswords(connection);
                EnsureUserIndexes(connection);
            }
        }

        /// <summary>
        /// Bazadagi eng oxirgi saqlangan kursni olish
        /// </summary>
        public CurrencyRate? GetLastCurrencyRate()
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Rate, Date FROM CurrencyRates ORDER BY Date DESC LIMIT 1";

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    string rawDate = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    DateTime parsedDate = DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue)
                        ? dateValue
                        : DateTime.MinValue;

                    return new CurrencyRate
                    {
                        Rate = reader.GetDouble(0),
                        Date = parsedDate
                    };
                }
            }
        }

        /// <summary>
        /// Yangi kursni bazaga saqlash
        /// </summary>
        public void SaveCurrencyRate(CurrencyRate rate)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO CurrencyRates (Rate, Date) VALUES (@rate, @date)";
                cmd.Parameters.AddWithValue("@rate", rate.Rate);
                cmd.Parameters.AddWithValue("@date", rate.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Loginni tekshirish
        /// </summary>
        public bool CheckLogin(string username, string password)
        {
            return AuthenticateUser(username, password) != null;
        }

        public AppUser? AuthenticateUser(string username, string password)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Username, PasswordHash, PasswordSalt, Role, MustChangePassword, Password
                    FROM Users
                    WHERE Username=@u
                    LIMIT 1";
                cmd.Parameters.AddWithValue("@u", username.Trim());

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                int id = reader.GetInt32(0);
                string foundUsername = reader.GetString(1);
                string hash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                string salt = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                string role = reader.IsDBNull(4) ? RoleSeller : reader.GetString(4);
                bool mustChangePassword = !reader.IsDBNull(5) && reader.GetInt32(5) == 1;
                string legacyPassword = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);

                bool valid = false;
                if (!string.IsNullOrWhiteSpace(hash) && !string.IsNullOrWhiteSpace(salt))
                {
                    try
                    {
                        valid = VerifyPassword(password, hash, salt);
                    }
                    catch
                    {
                        valid = false;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(legacyPassword) && string.Equals(legacyPassword, password, StringComparison.Ordinal))
                {
                    // Legacy plain-text parolni birinchi muvaffaqiyatli login vaqtida hashga o'tkazamiz.
                    SetPassword(connection, id, password, mustChangePassword);
                    valid = true;
                }

                if (!valid)
                {
                    return null;
                }

                return new AppUser
                {
                    Id = id,
                    Username = foundUsername,
                    Role = role,
                    MustChangePassword = mustChangePassword
                };
            }
        }

        /// <summary>
        /// Admin login va parolni yangilash
        /// </summary>
        public void UpdateAdminCredentials(string newUsername, string newPassword)
        {
            ValidatePassword(newPassword);

            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using var tx = connection.BeginTransaction();

                var targetCmd = connection.CreateCommand();
                targetCmd.CommandText = "SELECT Id FROM Users WHERE Role=@role LIMIT 1";
                targetCmd.Parameters.AddWithValue("@role", RoleAdmin);
                object? idObj = targetCmd.ExecuteScalar();
                if (idObj == null || idObj == DBNull.Value)
                {
                    throw new Exception("Admin foydalanuvchi topilmadi.");
                }

                int id = Convert.ToInt32(idObj);
                SetPassword(connection, id, newPassword, false);

                var renameCmd = connection.CreateCommand();
                renameCmd.CommandText = "UPDATE Users SET Username = @newUsername WHERE Id = @id";
                renameCmd.Parameters.AddWithValue("@newUsername", newUsername.Trim());
                renameCmd.Parameters.AddWithValue("@id", id);
                renameCmd.ExecuteNonQuery();

                tx.Commit();
            }
        }

        public List<AppUser> GetAllUsers()
        {
            var users = new List<AppUser>();
            using var connection = Database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, Role, MustChangePassword FROM Users ORDER BY Id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new AppUser
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Role = reader.IsDBNull(2) ? RoleSeller : reader.GetString(2),
                    MustChangePassword = !reader.IsDBNull(3) && reader.GetInt32(3) == 1
                });
            }

            return users;
        }

        public void AddUser(string username, string password, string role)
        {
            ValidateUserInput(username, password, role);
            ValidatePassword(password);
            var hashData = HashPassword(password);

            using var connection = Database.GetConnection();
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Users (Username, PasswordHash, PasswordSalt, MustChangePassword, Role, Password)
                VALUES (@u, @h, @s, @m, @r, '')";
            cmd.Parameters.AddWithValue("@u", username.Trim());
            cmd.Parameters.AddWithValue("@h", hashData.Hash);
            cmd.Parameters.AddWithValue("@s", hashData.Salt);
            cmd.Parameters.AddWithValue("@m", 1);
            cmd.Parameters.AddWithValue("@r", NormalizeRole(role));
            cmd.ExecuteNonQuery();
        }

        public void UpdateUser(int id, string username, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new Exception("Login bo'sh bo'lmasligi kerak.");
            }

            _ = NormalizeRole(role);

            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Username=@u, Role=@r WHERE Id=@id";
            cmd.Parameters.AddWithValue("@u", username.Trim());
            cmd.Parameters.AddWithValue("@r", NormalizeRole(role));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            if (!string.IsNullOrWhiteSpace(password))
            {
                ValidatePassword(password);
                SetPassword(connection, id, password, true);
            }

            tx.Commit();
        }

        public void DeleteUser(int id)
        {
            using var connection = Database.GetConnection();
            connection.Open();

            var roleCmd = connection.CreateCommand();
            roleCmd.CommandText = "SELECT Role FROM Users WHERE Id=@id";
            roleCmd.Parameters.AddWithValue("@id", id);
            object? roleObj = roleCmd.ExecuteScalar();
            string role = roleObj?.ToString() ?? string.Empty;

            if (string.Equals(role, RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                var countCmd = connection.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Role=@r";
                countCmd.Parameters.AddWithValue("@r", RoleAdmin);
                long adminCount = Convert.ToInt64(countCmd.ExecuteScalar());
                if (adminCount <= 1)
                {
                    throw new Exception("Oxirgi admin foydalanuvchini o'chirib bo'lmaydi.");
                }
            }

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Users WHERE Id=@id";
            deleteCmd.Parameters.AddWithValue("@id", id);
            deleteCmd.ExecuteNonQuery();
        }

        public void ChangePassword(int userId, string newPassword)
        {
            ValidatePassword(newPassword);

            using var connection = Database.GetConnection();
            connection.Open();
            SetPassword(connection, userId, newPassword, false);
        }

        public bool ResetPasswordByUsername(string username, string newPassword, bool mustChangePassword)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new Exception("Temporary parol bo'sh bo'lmasligi kerak.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            var find = connection.CreateCommand();
            find.CommandText = "SELECT Id FROM Users WHERE LOWER(Username)=LOWER(@u) LIMIT 1";
            find.Parameters.AddWithValue("@u", username.Trim());
            object? idObj = find.ExecuteScalar();
            if (idObj == null || idObj == DBNull.Value)
            {
                return false;
            }

            int userId = Convert.ToInt32(idObj);
            SetPassword(connection, userId, newPassword, mustChangePassword);
            return true;
        }

        private static bool UserExistsByRole(SqliteConnection connection, string role)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Role=@r";
            cmd.Parameters.AddWithValue("@r", role);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }

        private static string? EnsureRoleUser(SqliteConnection connection, string username, string role, string initialPassword)
        {
            if (UserExistsByRole(connection, role))
            {
                return null;
            }

            string effectivePassword = string.IsNullOrWhiteSpace(initialPassword) ? GenerateTemporaryPassword() : initialPassword.Trim();
            ValidatePassword(effectivePassword);
            var tempHash = HashPassword(effectivePassword);

            var existingCmd = connection.CreateCommand();
            existingCmd.CommandText = "SELECT Id FROM Users WHERE Username=@u LIMIT 1";
            existingCmd.Parameters.AddWithValue("@u", username);
            object? existingId = existingCmd.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE Users
                    SET Role=@r,
                        PasswordHash=@h,
                        PasswordSalt=@s,
                        Password='',
                        MustChangePassword=1
                    WHERE Id=@id";
                updateCmd.Parameters.AddWithValue("@r", role);
                updateCmd.Parameters.AddWithValue("@h", tempHash.Hash);
                updateCmd.Parameters.AddWithValue("@s", tempHash.Salt);
                updateCmd.Parameters.AddWithValue("@id", Convert.ToInt32(existingId));
                updateCmd.ExecuteNonQuery();
                return effectivePassword;
            }

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Users (Username, PasswordHash, PasswordSalt, MustChangePassword, Role, Password)
                VALUES (@u, @h, @s, 1, @r, '')";
            insertCmd.Parameters.AddWithValue("@u", username);
            insertCmd.Parameters.AddWithValue("@h", tempHash.Hash);
            insertCmd.Parameters.AddWithValue("@s", tempHash.Salt);
            insertCmd.Parameters.AddWithValue("@r", role);
            insertCmd.ExecuteNonQuery();
            return effectivePassword;
        }

        private static void ValidateUserInput(string username, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new Exception("Login bo'sh bo'lmasligi kerak.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new Exception("Parol bo'sh bo'lmasligi kerak.");
            }

            _ = NormalizeRole(role);
        }

        private static string NormalizeRole(string role)
        {
            if (string.Equals(role, RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return RoleAdmin;
            }

            if (string.Equals(role, RoleSeller, StringComparison.OrdinalIgnoreCase))
            {
                return RoleSeller;
            }

            throw new Exception("Noto'g'ri rol tanlandi.");
        }

        private static string GetSetting(string key, string fallback)
        {
            string? value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string GetOptionalSetting(string key)
        {
            string? value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new Exception("Parol bo'sh bo'lmasligi kerak.");
            }

            if (password.Length < 8)
            {
                throw new Exception("Parol kamida 8 ta belgidan iborat bo'lishi kerak.");
            }
        }

        private static (string Hash, string Salt) HashPassword(string password)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, PasswordIterations, HashAlgorithmName.SHA256);
            byte[] hashBytes = pbkdf2.GetBytes(32);

            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        private static bool VerifyPassword(string password, string hash, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, PasswordIterations, HashAlgorithmName.SHA256);
            byte[] newHash = pbkdf2.GetBytes(32);
            byte[] existingHash = Convert.FromBase64String(hash);
            return CryptographicOperations.FixedTimeEquals(newHash, existingHash);
        }

        private static void SetPassword(SqliteConnection connection, int userId, string password, bool mustChangePassword)
        {
            var hashData = HashPassword(password);
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users
                SET PasswordHash=@h,
                    PasswordSalt=@s,
                    Password='',
                    MustChangePassword=@m
                WHERE Id=@id";
            cmd.Parameters.AddWithValue("@h", hashData.Hash);
            cmd.Parameters.AddWithValue("@s", hashData.Salt);
            cmd.Parameters.AddWithValue("@m", mustChangePassword ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.ExecuteNonQuery();
        }

        private static void MigrateLegacyPasswords(SqliteConnection connection)
        {
            var usersToMigrate = new List<(int Id, string Password)>();
            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = @"
                SELECT Id, Password
                FROM Users
                WHERE (PasswordHash IS NULL OR TRIM(PasswordHash) = '' OR PasswordSalt IS NULL OR TRIM(PasswordSalt) = '')
                  AND Password IS NOT NULL
                  AND TRIM(Password) <> ''";

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    usersToMigrate.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            foreach (var row in usersToMigrate)
            {
                SetPassword(connection, row.Id, row.Password, false);
            }
        }

        private static void EnsureUserIndexes(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Users_Role ON Users(Role);";
            cmd.ExecuteNonQuery();
        }

        private static void TryAddColumn(SqliteConnection connection, string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Column mavjud bo'lsa ignore qilinadi.
            }
        }

        private static string GenerateTemporaryPassword()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(9);
            string raw = Convert.ToBase64String(bytes);
            return raw.Replace("+", "A").Replace("/", "B").Replace("=", "9") + "x1";
        }
    }
}
