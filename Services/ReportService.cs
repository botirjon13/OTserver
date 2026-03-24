using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using System;

namespace SantexnikaSRM.Services
{
    public class ReportService
    {
        public (double totalSales, double totalProfit, double totalExpenses, double netProfit) GetMonthlyReport(DateTime from, DateTime to, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanViewReports(currentUser),
                "Hisobotni ko'rish huquqi mavjud emas.");

            double sales = 0;
            double profit = 0;
            double expenses = 0;

            // Sanalarni SQLite tushunadigan ISO formatga o'tkazamiz
            string startDate = from.ToString("yyyy-MM-dd 00:00:00");
            string endDate = to.ToString("yyyy-MM-dd 23:59:59");

            using (var connection = Database.GetConnection())
            {
                connection.Open();

                // 1. Jami Sotuv va Foydani hisoblash
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(TotalUZS), SUM(ProfitUZS) FROM Sales WHERE Date BETWEEN @from AND @to";
                    cmd.Parameters.AddWithValue("@from", startDate);
                    cmd.Parameters.AddWithValue("@to", endDate);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            sales = reader.IsDBNull(0) ? 0 : reader.GetDouble(0);
                            profit = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        }
                    }
                }

                // 2. Jami Xarajatlarni (Rasxod) hisoblash
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(AmountUZS) FROM Expenses WHERE Date BETWEEN @from AND @to";
                    cmd.Parameters.AddWithValue("@from", startDate);
                    cmd.Parameters.AddWithValue("@to", endDate);

                    var result = cmd.ExecuteScalar();
                    expenses = (result == DBNull.Value || result == null) ? 0 : Convert.ToDouble(result);
                }
            }

            return (
                totalSales: sales,
                totalProfit: profit,
                totalExpenses: expenses,
                netProfit: profit - expenses
            );
        }
    }
}
