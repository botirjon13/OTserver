using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public class CustomerService
    {
        public List<Customer> GetAll()
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, FullName, Phone, Note, CreatedAt
                FROM Customers
                ORDER BY FullName ASC";

            var result = new List<Customer>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Customer
                {
                    Id = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    Phone = reader.GetString(2),
                    Note = reader.GetString(3),
                    CreatedAt = ParseDateTime(reader.GetString(4))
                });
            }

            return result;
        }

        public Customer? GetById(int id)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, FullName, Phone, Note, CreatedAt
                FROM Customers
                WHERE Id=@id
                LIMIT 1";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new Customer
            {
                Id = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Phone = reader.GetString(2),
                Note = reader.GetString(3),
                CreatedAt = ParseDateTime(reader.GetString(4))
            };
        }

        public int FindOrCreate(string fullName, string phone, string note)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            int id = FindOrCreate(connection, tx, fullName, phone, note);
            tx.Commit();
            return id;
        }

        public int FindOrCreate(SqliteConnection connection, SqliteTransaction transaction, string fullName, string phone, string note)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new Exception("Mijoz nomi bo'sh bo'lmasligi kerak.");
            }

            string cleanName = fullName.Trim();
            string cleanPhone = (phone ?? string.Empty).Trim();
            string cleanNote = (note ?? string.Empty).Trim();

            var find = connection.CreateCommand();
            find.Transaction = transaction;
            if (!string.IsNullOrWhiteSpace(cleanPhone))
            {
                find.CommandText = "SELECT Id FROM Customers WHERE LOWER(Phone)=@phone LIMIT 1";
                find.Parameters.AddWithValue("@phone", cleanPhone.ToLowerInvariant());
            }
            else
            {
                find.CommandText = "SELECT Id FROM Customers WHERE LOWER(FullName)=@name LIMIT 1";
                find.Parameters.AddWithValue("@name", cleanName.ToLowerInvariant());
            }

            object? existing = find.ExecuteScalar();
            if (existing != null)
            {
                int existingId = Convert.ToInt32(existing, CultureInfo.InvariantCulture);
                var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE Customers SET FullName=@name, Phone=@phone, Note=@note WHERE Id=@id";
                update.Parameters.AddWithValue("@name", cleanName);
                update.Parameters.AddWithValue("@phone", cleanPhone);
                update.Parameters.AddWithValue("@note", cleanNote);
                update.Parameters.AddWithValue("@id", existingId);
                update.ExecuteNonQuery();
                return existingId;
            }

            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO Customers (FullName, Phone, Note, CreatedAt)
                VALUES (@name, @phone, @note, @createdAt);
                SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@name", cleanName);
            insert.Parameters.AddWithValue("@phone", cleanPhone);
            insert.Parameters.AddWithValue("@note", cleanNote);
            insert.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            int newId = Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
            return newId;
        }

        public void UpdateCustomer(int id, string fullName, string phone, string note)
        {
            if (id <= 0)
            {
                throw new Exception("Mijoz identifikatori noto'g'ri.");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new Exception("Mijoz nomi bo'sh bo'lmasligi kerak.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Customers
                SET FullName=@name, Phone=@phone, Note=@note
                WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", fullName.Trim());
            cmd.Parameters.AddWithValue("@phone", (phone ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("@note", (note ?? string.Empty).Trim());
            int affected = cmd.ExecuteNonQuery();
            if (affected == 0)
            {
                throw new Exception("Mijoz topilmadi.");
            }
        }

        public void DeleteCustomer(int id)
        {
            if (id <= 0)
            {
                throw new Exception("Mijoz identifikatori noto'g'ri.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var usageCmd = connection.CreateCommand();
            usageCmd.Transaction = tx;
            usageCmd.CommandText = @"
                SELECT
                    (SELECT COUNT(*) FROM SaleCustomers WHERE CustomerId=@id),
                    (SELECT COUNT(*) FROM Debts WHERE CustomerId=@id)";
            usageCmd.Parameters.AddWithValue("@id", id);

            long saleLinks = 0;
            long debtLinks = 0;
            using (var reader = usageCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    saleLinks = reader.GetInt64(0);
                    debtLinks = reader.GetInt64(1);
                }
            }

            if (saleLinks > 0 || debtLinks > 0)
            {
                throw new Exception("Bu mijoz savdo/qarz tarixiga bog'langan, o'chirib bo'lmaydi.");
            }

            var deleteCmd = connection.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = "DELETE FROM Customers WHERE Id=@id";
            deleteCmd.Parameters.AddWithValue("@id", id);
            int affected = deleteCmd.ExecuteNonQuery();
            if (affected == 0)
            {
                throw new Exception("Mijoz topilmadi.");
            }

            tx.Commit();
        }

        public void AttachCustomerToSale(int saleId, int customerId)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            AttachCustomerToSale(connection, tx, saleId, customerId);
            tx.Commit();
        }

        public void AttachCustomerToSale(SqliteConnection connection, SqliteTransaction transaction, int saleId, int customerId)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT OR REPLACE INTO SaleCustomers (SaleId, CustomerId, CreatedAt)
                VALUES (@saleId, @customerId, @createdAt)";
            cmd.Parameters.AddWithValue("@saleId", saleId);
            cmd.Parameters.AddWithValue("@customerId", customerId);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        public int GetTotalCustomerCount()
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Customers";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public int GetDebtorCustomerCount()
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(DISTINCT CustomerId) FROM Debts WHERE RemainingAmountUZS > 0";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public List<CustomerOverview> GetOverview(string searchText, bool onlyDebtors)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    c.Id,
                    c.FullName,
                    c.Phone,
                    IFNULL(sc.SalesCount, 0) AS SalesCount,
                    IFNULL(d.OpenDebtCount, 0) AS OpenDebtCount,
                    IFNULL(d.OutstandingUZS, 0) AS OutstandingUZS
                FROM Customers c
                LEFT JOIN (
                    SELECT CustomerId, COUNT(*) AS SalesCount
                    FROM SaleCustomers
                    GROUP BY CustomerId
                ) sc ON sc.CustomerId = c.Id
                LEFT JOIN (
                    SELECT CustomerId,
                           SUM(CASE WHEN RemainingAmountUZS > 0 THEN 1 ELSE 0 END) AS OpenDebtCount,
                           SUM(CASE WHEN RemainingAmountUZS > 0 THEN RemainingAmountUZS ELSE 0 END) AS OutstandingUZS
                    FROM Debts
                    GROUP BY CustomerId
                ) d ON d.CustomerId = c.Id
                WHERE (@search = '' OR LOWER(c.FullName) LIKE @searchLike OR LOWER(c.Phone) LIKE @searchLike)
                  AND (@onlyDebtors = 0 OR IFNULL(d.OpenDebtCount, 0) > 0)
                ORDER BY c.FullName ASC";

            string search = (searchText ?? string.Empty).Trim().ToLowerInvariant();
            cmd.Parameters.AddWithValue("@search", search);
            cmd.Parameters.AddWithValue("@searchLike", $"%{search}%");
            cmd.Parameters.AddWithValue("@onlyDebtors", onlyDebtors ? 1 : 0);

            var rows = new List<CustomerOverview>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new CustomerOverview
                {
                    Id = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    Phone = reader.GetString(2),
                    SalesCount = reader.GetInt32(3),
                    OpenDebtCount = reader.GetInt32(4),
                    OutstandingUZS = reader.GetDouble(5)
                });
            }

            return rows;
        }

        private static DateTime ParseDateTime(string raw)
        {
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.Parse(raw, CultureInfo.InvariantCulture);
        }
    }
}
