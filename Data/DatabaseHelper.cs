using System;
using System.Configuration;
using System.Globalization;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Data
{
    public class DatabaseHelper
    {
        private const string DefaultAdminUsername = "admin";
        private const string DefaultAdminPassword = "1234";
        private const string DefaultSellerUsername = "seller";
        private const string DefaultSellerPassword = "1234";
        private const string AdminUsernameKey = "AdminUsername";
        private const string AdminPasswordKey = "AdminPassword";
        private const string SellerUsernameKey = "SellerUsername";
        private const string SellerPasswordKey = "SellerPassword";
        public const string RoleAdmin = "Admin";
        public const string RoleSeller = "Seller";

        /// <summary>
        /// Bazada foydalanuvchi bo'lmasa, admin yaratish
        /// </summary>
        public void CreateDefaultUser()
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                string configuredUsername = GetSetting(AdminUsernameKey, DefaultAdminUsername);
                string configuredPassword = GetSetting(AdminPasswordKey, DefaultAdminPassword);
                string configuredSellerUsername = GetSetting(SellerUsernameKey, DefaultSellerUsername);
                string configuredSellerPassword = GetSetting(SellerPasswordKey, DefaultSellerPassword);

                EnsureRoleUser(connection, configuredUsername, configuredPassword, RoleAdmin);
                EnsureRoleUser(connection, configuredSellerUsername, configuredSellerPassword, RoleSeller);
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
                    Password TEXT NOT NULL,
                    Role TEXT NOT NULL DEFAULT 'Seller'
                );";

                var cmd = connection.CreateCommand();
                cmd.CommandText = usersTable;
                cmd.ExecuteNonQuery();

                // Legacy baza uchun migratsiya.
                var migrateCmd = connection.CreateCommand();
                migrateCmd.CommandText = "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'Seller'";
                try
                {
                    migrateCmd.ExecuteNonQuery();
                }
                catch
                {
                    // Column mavjud bo'lsa ignore qilinadi.
                }
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
                cmd.CommandText = "SELECT Id, Username, Password, Role FROM Users WHERE Username=@u AND Password=@p LIMIT 1";
                cmd.Parameters.AddWithValue("@u", username.Trim());
                cmd.Parameters.AddWithValue("@p", password);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                return new AppUser
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Password = reader.GetString(2),
                    Role = reader.IsDBNull(3) ? RoleSeller : reader.GetString(3)
                };
            }
        }

        /// <summary>
        /// Admin login va parolni yangilash
        /// </summary>
        public void UpdateAdminCredentials(string newUsername, string newPassword)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Users SET Username = @newUsername, Password = @newPassword WHERE Role = @role";
                cmd.Parameters.AddWithValue("@newUsername", newUsername);
                cmd.Parameters.AddWithValue("@newPassword", newPassword);
                cmd.Parameters.AddWithValue("@role", RoleAdmin);
                cmd.ExecuteNonQuery();
            }
        }

        public List<AppUser> GetAllUsers()
        {
            var users = new List<AppUser>();
            using var connection = Database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, Password, Role FROM Users ORDER BY Id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new AppUser
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Password = reader.GetString(2),
                    Role = reader.IsDBNull(3) ? RoleSeller : reader.GetString(3)
                });
            }

            return users;
        }

        public void AddUser(string username, string password, string role)
        {
            ValidateUserInput(username, password, role);

            using var connection = Database.GetConnection();
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Users (Username, Password, Role) VALUES (@u, @p, @r)";
            cmd.Parameters.AddWithValue("@u", username.Trim());
            cmd.Parameters.AddWithValue("@p", password);
            cmd.Parameters.AddWithValue("@r", NormalizeRole(role));
            cmd.ExecuteNonQuery();
        }

        public void UpdateUser(int id, string username, string password, string role)
        {
            ValidateUserInput(username, password, role);

            using var connection = Database.GetConnection();
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Username=@u, Password=@p, Role=@r WHERE Id=@id";
            cmd.Parameters.AddWithValue("@u", username.Trim());
            cmd.Parameters.AddWithValue("@p", password);
            cmd.Parameters.AddWithValue("@r", NormalizeRole(role));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
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

        private static bool UserExistsByRole(SqliteConnection connection, string role)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Role=@r";
            cmd.Parameters.AddWithValue("@r", role);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }

        private static void EnsureRoleUser(SqliteConnection connection, string username, string password, string role)
        {
            if (UserExistsByRole(connection, role))
            {
                return;
            }

            var existingCmd = connection.CreateCommand();
            existingCmd.CommandText = "SELECT Id FROM Users WHERE Username=@u LIMIT 1";
            existingCmd.Parameters.AddWithValue("@u", username);
            object? existingId = existingCmd.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = "UPDATE Users SET Password=@p, Role=@r WHERE Id=@id";
                updateCmd.Parameters.AddWithValue("@p", password);
                updateCmd.Parameters.AddWithValue("@r", role);
                updateCmd.Parameters.AddWithValue("@id", Convert.ToInt32(existingId));
                updateCmd.ExecuteNonQuery();
                return;
            }

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Users (Username, Password, Role) VALUES (@u, @p, @r)";
            insertCmd.Parameters.AddWithValue("@u", username);
            insertCmd.Parameters.AddWithValue("@p", password);
            insertCmd.Parameters.AddWithValue("@r", role);
            insertCmd.ExecuteNonQuery();
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
    }
}
