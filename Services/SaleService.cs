using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace SantexnikaSRM.Services
{
    public class SaleService
    {
        public int CreateSale(Sale sale, AppUser currentUser)
        {
            return CreateSale(sale, currentUser, null);
        }

        public int CreateSale(
            Sale sale,
            AppUser currentUser,
            Action<SqliteConnection, SqliteTransaction, int>? inTransactionActions)
        {
            AuthorizationService.Require(
                AuthorizationService.CanCreateSales(currentUser),
                "Sotuv yaratish huquqi mavjud emas.");

            if (sale.Items == null || sale.Items.Count == 0)
            {
                throw new Exception("Sotuv uchun kamida bitta mahsulot bo'lishi kerak.");
            }

            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        double totalProfit = 0;
                        double netLineTotal = 0;
                        double itemDiscountTotal = 0;

                        foreach (var item in sale.Items)
                        {
                            if (item.Quantity <= 0 || item.SellPriceUZS <= 0)
                            {
                                throw new Exception("Miqdor va narx musbat son bo'lishi kerak.");
                            }

                            var productCmd = connection.CreateCommand();
                            productCmd.Transaction = transaction;
                            productCmd.CommandText = "SELECT PurchasePriceUZS, QuantityUSD FROM Products WHERE Id = @id";
                            productCmd.Parameters.AddWithValue("@id", item.ProductId);
                            
                            double purchasePriceUZS = 0;
                            double currentQuantity = 0;
                            using (var reader = productCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    purchasePriceUZS = reader.GetDouble(0);
                                    currentQuantity = reader.GetDouble(1);
                                }
                                else
                                {
                                    throw new Exception($"Mahsulot topilmadi (Id={item.ProductId}).");
                                }
                            }

                            if (currentQuantity < item.Quantity)
                            {
                                throw new Exception($"Mahsulot yetarli emas (Id={item.ProductId}). Omborda: {currentQuantity}, so'ralgan: {item.Quantity}.");
                            }

                            if (item.DiscountUZS < 0)
                            {
                                throw new Exception("Chegirma manfiy bo'lmasligi kerak.");
                            }

                            double lineNetTotal = item.SellPriceUZS * item.Quantity;
                            double profitPerItem = item.SellPriceUZS - purchasePriceUZS;

                            netLineTotal += lineNetTotal;
                            itemDiscountTotal += item.DiscountUZS;
                            totalProfit += (profitPerItem * item.Quantity);

                            var updateQtyCmd = connection.CreateCommand();
                            updateQtyCmd.Transaction = transaction;
                            updateQtyCmd.CommandText = "UPDATE Products SET QuantityUSD = QuantityUSD - @qty WHERE Id = @id";
                            updateQtyCmd.Parameters.AddWithValue("@qty", item.Quantity);
                            updateQtyCmd.Parameters.AddWithValue("@id", item.ProductId);
                            updateQtyCmd.ExecuteNonQuery();
                        }

                        double totalDiscount = sale.DiscountUZS > 0 ? sale.DiscountUZS : itemDiscountTotal;
                        double subtotalSales = sale.SubtotalUZS > 0 ? sale.SubtotalUZS : netLineTotal + totalDiscount;
                        double requestedFinalTotal = sale.TotalUZS > 0 ? sale.TotalUZS : netLineTotal;
                        double calculatedFinalTotal = Math.Max(0, subtotalSales - totalDiscount);

                        if (subtotalSales + 0.000001 < totalDiscount)
                        {
                            throw new Exception("Chegirma jami summadan oshmasligi kerak.");
                        }

                        if (Math.Abs(itemDiscountTotal - totalDiscount) > 1.0)
                        {
                            throw new Exception("Umumiy chegirma va item chegirmalari mos emas. Sotuv qayta hisoblab ko'ring.");
                        }

                        if (Math.Abs(netLineTotal - requestedFinalTotal) > 1.0 ||
                            Math.Abs(calculatedFinalTotal - requestedFinalTotal) > 1.0)
                        {
                            throw new Exception("Jami summa va item summalari mos emas. Sotuv qayta hisoblab ko'ring.");
                        }

                        var saleCmd = connection.CreateCommand();
                        saleCmd.Transaction = transaction;
                        saleCmd.CommandText = @"
                            INSERT INTO Sales (Date, TotalUZS, SubtotalUZS, DiscountType, DiscountValue, DiscountUZS, ProfitUZS)
                            VALUES (@date, @total, @subtotal, @discountType, @discountValue, @discount, @profit);
                            SELECT last_insert_rowid();";

                        saleCmd.Parameters.AddWithValue("@date", sale.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                        saleCmd.Parameters.AddWithValue("@total", requestedFinalTotal);
                        saleCmd.Parameters.AddWithValue("@subtotal", subtotalSales);
                        saleCmd.Parameters.AddWithValue("@discountType", string.IsNullOrWhiteSpace(sale.DiscountType) ? "None" : sale.DiscountType);
                        saleCmd.Parameters.AddWithValue("@discountValue", sale.DiscountValue);
                        saleCmd.Parameters.AddWithValue("@discount", totalDiscount);
                        saleCmd.Parameters.AddWithValue("@profit", totalProfit);

                        long saleId = Convert.ToInt64(saleCmd.ExecuteScalar());

                        foreach (var item in sale.Items)
                        {
                            var itemCmd = connection.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = @"
                                INSERT INTO SaleItems (SaleId, ProductId, Quantity, SellPriceUZS, DiscountUZS)
                                VALUES (@saleId, @productId, @qty, @price, @discount)";

                            itemCmd.Parameters.AddWithValue("@saleId", saleId);
                            itemCmd.Parameters.AddWithValue("@productId", item.ProductId);
                            itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                            itemCmd.Parameters.AddWithValue("@price", item.SellPriceUZS);
                            itemCmd.Parameters.AddWithValue("@discount", item.DiscountUZS);
                            itemCmd.ExecuteNonQuery();
                        }

                        inTransactionActions?.Invoke(connection, transaction, Convert.ToInt32(saleId));

                        transaction.Commit();
                        return Convert.ToInt32(saleId);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // MANA SHU FUNKSIYA HISOBOTLARNI TO'G'RI KO'RSATISH UCHUN KERAK:
        public List<Sale> GetSalesByDateRange(DateTime from, DateTime to)
        {
            var list = new List<Sale>();
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    // Bugun 16-dan 16-gacha deganda 16-kunning oxirigacha bo'lgan hamma narsani oladi
                    string startDate = from.Date.ToString("yyyy-MM-dd 00:00:00");
                    string endDate = to.Date.ToString("yyyy-MM-dd 23:59:59");

                    cmd.CommandText = @"
                        SELECT Id, Date, TotalUZS, ProfitUZS,
                               IFNULL(SubtotalUZS, TotalUZS),
                               IFNULL(DiscountType, 'None'),
                               IFNULL(DiscountValue, 0),
                               IFNULL(DiscountUZS, 0)
                        FROM Sales
                        WHERE Date BETWEEN @from AND @to
                        ORDER BY Date DESC";
                    cmd.Parameters.AddWithValue("@from", startDate);
                    cmd.Parameters.AddWithValue("@to", endDate);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Sale
                            {
                                Id = reader.GetInt32(0),
                                Date = ParseDateTime(reader.GetString(1)),
                                TotalUZS = reader.GetDouble(2),
                                ProfitUZS = reader.GetDouble(3),
                                SubtotalUZS = reader.GetDouble(4),
                                DiscountType = reader.GetString(5),
                                DiscountValue = reader.GetDouble(6),
                                DiscountUZS = reader.GetDouble(7)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public double GetCurrentRate()
        {
            using var connection = Database.GetConnection();
            connection.Open();
            return GetLatestRate(connection);
        }

        public List<SaleReportRow> GetSaleReportRows(DateTime from, DateTime to, double usdRate, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanViewReports(currentUser),
                "Sotuv hisobotini ko'rish huquqi mavjud emas.");

            var rows = new List<SaleReportRow>();

            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();

            string startDate = from.Date.ToString("yyyy-MM-dd 00:00:00");
            string endDate = to.Date.ToString("yyyy-MM-dd 23:59:59");

            cmd.CommandText = @"
                WITH returned AS (
                    SELECT ri.SaleItemId,
                           SUM(ri.Quantity) AS ReturnedQuantity
                    FROM ReturnItems ri
                    INNER JOIN Returns r ON r.Id = ri.ReturnId
                    GROUP BY ri.SaleItemId
                )
                SELECT p.Name,
                       si.Quantity,
                       IFNULL(returned.ReturnedQuantity, 0),
                       si.SellPriceUZS,
                       p.PurchasePriceUZS
                FROM SaleItems si
                INNER JOIN Sales s ON s.Id = si.SaleId
                INNER JOIN Products p ON p.Id = si.ProductId
                LEFT JOIN returned ON returned.SaleItemId = si.Id
                WHERE s.Date BETWEEN @from AND @to
                ORDER BY s.Date DESC";

            cmd.Parameters.AddWithValue("@from", startDate);
            cmd.Parameters.AddWithValue("@to", endDate);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string productName = reader.GetString(0);
                double quantity = reader.GetDouble(1);
                double returnedQuantity = reader.GetDouble(2);
                double sellPriceUzs = reader.GetDouble(3);
                double purchasePriceUzs = reader.GetDouble(4);
                double netQuantity = Math.Max(0, quantity - returnedQuantity);

                double soldAmountUzs = sellPriceUzs * netQuantity;
                double soldAmountUsd = usdRate > 0 ? soldAmountUzs / usdRate : 0;

                double purchaseAmountUzs = purchasePriceUzs * netQuantity;
                double profitUzs = soldAmountUzs - purchaseAmountUzs;
                double profitUsd = usdRate > 0 ? profitUzs / usdRate : 0;

                rows.Add(new SaleReportRow
                {
                    ProductName = productName,
                    Quantity = netQuantity,
                    SoldAmountUSD = soldAmountUsd,
                    SoldAmountUZS = soldAmountUzs,
                    ProfitUSD = profitUsd,
                    ProfitUZS = profitUzs
                });
            }

            return rows;
        }

        private double GetLatestRate(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Rate FROM CurrencyRates ORDER BY Date DESC LIMIT 1";
            var result = cmd.ExecuteScalar();

            if (result != null) return Convert.ToDouble(result);

            string? defaultRate = ConfigurationManager.AppSettings["DefaultDollarRate"];
            return string.IsNullOrWhiteSpace(defaultRate) ? 12800 : Convert.ToDouble(defaultRate);
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
