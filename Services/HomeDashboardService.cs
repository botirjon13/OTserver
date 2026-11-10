using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public sealed class HomeDashboardService
    {
        private readonly ReportService _reportService = new ReportService();
        private readonly DebtService _debtService = new DebtService();
        private readonly CustomerService _customerService = new CustomerService();
        private readonly ProductService _productService = new ProductService();

        public HomeDashboardSnapshot GetSnapshot(AppUser currentUser, DateTime today)
        {
            bool isAdmin = AuthorizationService.CanViewReports(currentUser);
            bool canManageDebts = AuthorizationService.CanManageDebts(currentUser);
            AuthorizationService.Require(
                isAdmin || canManageDebts,
                "Bosh sahifa ko'rsatkichlarini ko'rish huquqi mavjud emas.");

            DebtSummary debts = canManageDebts
                ? _debtService.GetSummary(currentUser)
                : new DebtSummary();

            HomeDashboardSnapshot snapshot = new HomeDashboardSnapshot
            {
                IsAdministrator = isAdmin,
                CustomerCount = canManageDebts ? _customerService.GetTotalCustomerCount() : 0,
                OutstandingUZS = debts.OutstandingUZS,
                OverdueUZS = debts.OverdueUZS,
                OpenDebts = debts.OpenDebts,
                OverdueDebts = debts.OverdueDebts
            };

            if (!isAdmin)
            {
                return snapshot;
            }

            (snapshot.TodaySalesUZS, snapshot.TodayProfitUZS, snapshot.TodayExpensesUZS, snapshot.TodayNetUZS) =
                _reportService.GetMonthlyReport(today.Date, today.Date, currentUser);

            List<Product> products = _productService.GetAll();
            snapshot.ProductCount = products.Count;
            snapshot.LowStockCount = products.Count(product => product.QuantityUSD <= 5);
            snapshot.Week = GetWeekMetrics(today.Date);
            return snapshot;
        }

        private static List<HomeDashboardDay> GetWeekMetrics(DateTime today)
        {
            DateTime start = today.AddDays(-6);
            DateTime end = today.AddDays(1);
            var days = new Dictionary<DateTime, HomeDashboardDay>();

            for (DateTime date = start; date < end; date = date.AddDays(1))
            {
                days.Add(date, new HomeDashboardDay { Date = date });
            }

            using SqliteConnection connection = Database.GetConnection();
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
SELECT Day, SUM(SalesUZS), SUM(ProfitUZS), SUM(ExpensesUZS)
FROM (
    SELECT substr(Date, 1, 10) AS Day, TotalUZS AS SalesUZS, ProfitUZS, 0 AS ExpensesUZS
    FROM Sales
    WHERE Date >= @start AND Date < @end
    UNION ALL
    SELECT substr(Date, 1, 10) AS Day, 0 AS SalesUZS, 0 AS ProfitUZS, AmountUZS AS ExpensesUZS
    FROM Expenses
    WHERE Date >= @start AND Date < @end
)
GROUP BY Day
ORDER BY Day";
            command.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime date = DateTime.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!days.TryGetValue(date, out HomeDashboardDay? day))
                {
                    continue;
                }

                day.SalesUZS = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                day.ProfitUZS = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                day.ExpensesUZS = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
            }

            return new List<HomeDashboardDay>(days.Values);
        }
    }

    public sealed class HomeDashboardSnapshot
    {
        public bool IsAdministrator { get; set; }
        public int CustomerCount { get; set; }
        public int ProductCount { get; set; }
        public int LowStockCount { get; set; }
        public int OpenDebts { get; set; }
        public int OverdueDebts { get; set; }
        public double OutstandingUZS { get; set; }
        public double OverdueUZS { get; set; }
        public double TodaySalesUZS { get; set; }
        public double TodayProfitUZS { get; set; }
        public double TodayExpensesUZS { get; set; }
        public double TodayNetUZS { get; set; }
        public List<HomeDashboardDay> Week { get; set; } = new List<HomeDashboardDay>();
    }

    public sealed class HomeDashboardDay
    {
        public DateTime Date { get; set; }
        public double SalesUZS { get; set; }
        public double ProfitUZS { get; set; }
        public double ExpensesUZS { get; set; }
    }
}
