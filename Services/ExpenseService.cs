using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SantexnikaSRM.Services
{
    public class ExpenseService
    {
        public void Add(Expense expense, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageExpenses(currentUser),
                "Rasxod qo'shish huquqi mavjud emas.");

            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO Expenses (Date, Type, Description, AmountUZS) VALUES (@date, @type, @desc, @amount)";
                    cmd.Parameters.AddWithValue("@date", expense.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@type", expense.Type ?? "");
                    cmd.Parameters.AddWithValue("@desc", expense.Description ?? "");
                    cmd.Parameters.AddWithValue("@amount", expense.AmountUZS);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<Expense> GetByDateRange(DateTime from, DateTime to, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanViewReports(currentUser),
                "Rasxod hisobotini ko'rish huquqi mavjud emas.");

            var list = new List<Expense>();
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    string startDate = from.Date.ToString("yyyy-MM-dd 00:00:00");
                    string endDate = to.Date.ToString("yyyy-MM-dd 23:59:59");

                    cmd.CommandText = "SELECT * FROM Expenses WHERE Date BETWEEN @from AND @to ORDER BY Date DESC";
                    cmd.Parameters.AddWithValue("@from", startDate);
                    cmd.Parameters.AddWithValue("@to", endDate);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Expense
                            {
                                Id = reader.GetInt32(0),
                                Date = ParseDateTime(reader.GetString(1)),
                                Type = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                AmountUZS = reader.GetDouble(4)
                            });
                        }
                    }
                }
            }

            return list;
        }

        public List<Expense> GetLatest(int count, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageExpenses(currentUser),
                "Rasxodlarni ko'rish huquqi mavjud emas.");

            var list = new List<Expense>();
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM Expenses ORDER BY Date DESC LIMIT @count";
                    cmd.Parameters.AddWithValue("@count", count);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Expense
                            {
                                Id = reader.GetInt32(0),
                                Date = ParseDateTime(reader.GetString(1)),
                                Type = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                AmountUZS = reader.GetDouble(4)
                            });
                        }
                    }
                }
            }

            return list;
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
