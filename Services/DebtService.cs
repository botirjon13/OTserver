using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public class DebtService
    {
        public int CreateDebtForSale(
            int saleId,
            int customerId,
            double initialPaymentUZS,
            DateTime dueDate,
            AppUser currentUser)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            int debtId = CreateDebtForSale(connection, tx, saleId, customerId, initialPaymentUZS, dueDate, currentUser);
            tx.Commit();
            return debtId;
        }

        public int CreateDebtForSale(
            SqliteConnection connection,
            SqliteTransaction transaction,
            int saleId,
            int customerId,
            double initialPaymentUZS,
            DateTime dueDate,
            AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageDebts(currentUser),
                "Qarz bilan ishlash huquqi mavjud emas.");

            if (customerId <= 0)
            {
                throw new Exception("Mijoz tanlanishi shart.");
            }

            if (initialPaymentUZS < 0)
            {
                throw new Exception("Boshlang'ich to'lov manfiy bo'lmasligi kerak.");
            }

            var saleCmd = connection.CreateCommand();
            saleCmd.Transaction = transaction;
            saleCmd.CommandText = "SELECT TotalUZS FROM Sales WHERE Id=@id LIMIT 1";
            saleCmd.Parameters.AddWithValue("@id", saleId);
            object? saleTotalObj = saleCmd.ExecuteScalar();
            if (saleTotalObj == null)
            {
                throw new Exception("Sotuv topilmadi.");
            }

            double total = Convert.ToDouble(saleTotalObj, CultureInfo.InvariantCulture);
            if (initialPaymentUZS > total)
            {
                throw new Exception("Boshlang'ich to'lov jami summadan oshmasligi kerak.");
            }

            var checkCmd = connection.CreateCommand();
            checkCmd.Transaction = transaction;
            checkCmd.CommandText = "SELECT COUNT(*) FROM Debts WHERE SaleId=@saleId";
            checkCmd.Parameters.AddWithValue("@saleId", saleId);
            long exists = Convert.ToInt64(checkCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (exists > 0)
            {
                throw new Exception("Ushbu sotuv uchun qarz allaqachon yaratilgan.");
            }

            double remaining = Math.Max(0, total - initialPaymentUZS);
            string status = BuildStatus(remaining, dueDate);
            string nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            var debtCmd = connection.CreateCommand();
            debtCmd.Transaction = transaction;
            debtCmd.CommandText = @"
                INSERT INTO Debts
                (SaleId, CustomerId, TotalAmountUZS, PaidAmountUZS, RemainingAmountUZS, DueDate, Status, CreatedAt)
                VALUES
                (@saleId, @customerId, @total, @paid, @remaining, @dueDate, @status, @createdAt);
                SELECT last_insert_rowid();";
            debtCmd.Parameters.AddWithValue("@saleId", saleId);
            debtCmd.Parameters.AddWithValue("@customerId", customerId);
            debtCmd.Parameters.AddWithValue("@total", total);
            debtCmd.Parameters.AddWithValue("@paid", initialPaymentUZS);
            debtCmd.Parameters.AddWithValue("@remaining", remaining);
            debtCmd.Parameters.AddWithValue("@dueDate", dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            debtCmd.Parameters.AddWithValue("@status", status);
            debtCmd.Parameters.AddWithValue("@createdAt", nowText);
            int debtId = Convert.ToInt32(debtCmd.ExecuteScalar(), CultureInfo.InvariantCulture);

            if (initialPaymentUZS > 0)
            {
                var paymentCmd = connection.CreateCommand();
                paymentCmd.Transaction = transaction;
                paymentCmd.CommandText = @"
                    INSERT INTO DebtPayments (DebtId, AmountUZS, PaymentType, Comment, PaymentDate)
                    VALUES (@debtId, @amount, @type, @comment, @date)";
                paymentCmd.Parameters.AddWithValue("@debtId", debtId);
                paymentCmd.Parameters.AddWithValue("@amount", initialPaymentUZS);
                paymentCmd.Parameters.AddWithValue("@type", "Boshlang'ich to'lov");
                paymentCmd.Parameters.AddWithValue("@comment", "Sotuv vaqtida qisman to'lov");
                paymentCmd.Parameters.AddWithValue("@date", nowText);
                paymentCmd.ExecuteNonQuery();
            }

            return debtId;
        }

        public List<Debt> GetDebts(string statusFilter, string search, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageDebts(currentUser),
                "Qarzdorlar ro'yxatini ko'rish huquqi mavjud emas.");

            RefreshStatuses();
            var result = new List<Debt>();

            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();

            cmd.CommandText = @"
                SELECT d.Id, d.SaleId, d.CustomerId, d.TotalAmountUZS, d.PaidAmountUZS, d.RemainingAmountUZS,
                       d.DueDate, d.Status, d.CreatedAt, c.FullName, c.Phone
                FROM Debts d
                INNER JOIN Customers c ON c.Id = d.CustomerId
                WHERE (@status = 'All' OR d.Status = @status)
                  AND (@search = '' OR LOWER(c.FullName) LIKE @searchLike OR LOWER(c.Phone) LIKE @searchLike)
                ORDER BY
                    CASE d.Status WHEN 'Overdue' THEN 0 WHEN 'Open' THEN 1 ELSE 2 END,
                    d.DueDate ASC,
                    d.Id DESC";
            string normalizedSearch = (search ?? string.Empty).Trim().ToLowerInvariant();
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(statusFilter) ? "All" : statusFilter);
            cmd.Parameters.AddWithValue("@search", normalizedSearch);
            cmd.Parameters.AddWithValue("@searchLike", $"%{normalizedSearch}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Debt
                {
                    Id = reader.GetInt32(0),
                    SaleId = reader.GetInt32(1),
                    CustomerId = reader.GetInt32(2),
                    TotalAmountUZS = reader.GetDouble(3),
                    PaidAmountUZS = reader.GetDouble(4),
                    RemainingAmountUZS = reader.GetDouble(5),
                    DueDate = ParseDate(reader.GetString(6)),
                    Status = reader.GetString(7),
                    CreatedAt = ParseDateTime(reader.GetString(8)),
                    CustomerFullName = reader.GetString(9),
                    CustomerPhone = reader.GetString(10)
                });
            }

            return result;
        }

        public DebtSummary GetSummary(AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageDebts(currentUser),
                "Qarzdorlar bo'limiga ruxsat yo'q.");

            RefreshStatuses();

            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    IFNULL(SUM(CASE WHEN Status IN ('Open', 'Overdue') THEN RemainingAmountUZS ELSE 0 END), 0),
                    IFNULL(SUM(CASE WHEN Status = 'Overdue' THEN RemainingAmountUZS ELSE 0 END), 0),
                    IFNULL(SUM(CASE WHEN Status = 'Open' THEN 1 ELSE 0 END), 0),
                    IFNULL(SUM(CASE WHEN Status = 'Overdue' THEN 1 ELSE 0 END), 0)
                FROM Debts";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new DebtSummary();
            }

            return new DebtSummary
            {
                OutstandingUZS = reader.GetDouble(0),
                OverdueUZS = reader.GetDouble(1),
                OpenDebts = reader.GetInt32(2),
                OverdueDebts = reader.GetInt32(3)
            };
        }

        public void AddPayment(int debtId, double amountUZS, string paymentType, string comment, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageDebts(currentUser),
                "Qarz to'lovini kiritish huquqi mavjud emas.");

            if (amountUZS <= 0)
            {
                throw new Exception("To'lov summasi musbat bo'lishi kerak.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            var readCmd = connection.CreateCommand();
            readCmd.Transaction = tx;
            readCmd.CommandText = "SELECT PaidAmountUZS, RemainingAmountUZS, DueDate FROM Debts WHERE Id=@id LIMIT 1";
            readCmd.Parameters.AddWithValue("@id", debtId);
            double paid;
            double remaining;
            DateTime dueDate;

            using (var reader = readCmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    throw new Exception("Qarz yozuvi topilmadi.");
                }

                paid = reader.GetDouble(0);
                remaining = reader.GetDouble(1);
                dueDate = ParseDate(reader.GetString(2));
            }

            if (amountUZS > remaining)
            {
                throw new Exception("To'lov summasi qarz qoldig'idan oshmasligi kerak.");
            }

            var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = @"
                INSERT INTO DebtPayments (DebtId, AmountUZS, PaymentType, Comment, PaymentDate)
                VALUES (@debtId, @amount, @type, @comment, @date)";
            insert.Parameters.AddWithValue("@debtId", debtId);
            insert.Parameters.AddWithValue("@amount", amountUZS);
            insert.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(paymentType) ? "Naqd" : paymentType.Trim());
            insert.Parameters.AddWithValue("@comment", comment?.Trim() ?? string.Empty);
            insert.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            insert.ExecuteNonQuery();

            double newPaid = paid + amountUZS;
            double newRemaining = Math.Max(0, remaining - amountUZS);
            string newStatus = BuildStatus(newRemaining, dueDate);

            var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = @"
                UPDATE Debts
                SET PaidAmountUZS=@paid, RemainingAmountUZS=@remaining, Status=@status
                WHERE Id=@id";
            update.Parameters.AddWithValue("@paid", newPaid);
            update.Parameters.AddWithValue("@remaining", newRemaining);
            update.Parameters.AddWithValue("@status", newStatus);
            update.Parameters.AddWithValue("@id", debtId);
            update.ExecuteNonQuery();

            tx.Commit();
        }

        private void RefreshStatuses()
        {
            using var connection = Database.GetConnection();
            connection.Open();

            string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Debts
                SET Status = CASE
                    WHEN RemainingAmountUZS <= 0 THEN 'Closed'
                    WHEN date(DueDate) < date(@today) THEN 'Overdue'
                    ELSE 'Open'
                END";
            cmd.Parameters.AddWithValue("@today", today);
            cmd.ExecuteNonQuery();
        }

        private static string BuildStatus(double remaining, DateTime dueDate)
        {
            if (remaining <= 0)
            {
                return "Closed";
            }

            return dueDate.Date < DateTime.Today ? "Overdue" : "Open";
        }

        private static DateTime ParseDateTime(string raw)
        {
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.Parse(raw, CultureInfo.InvariantCulture);
        }

        private static DateTime ParseDate(string raw)
        {
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.Parse(raw, CultureInfo.InvariantCulture);
        }
    }
}
