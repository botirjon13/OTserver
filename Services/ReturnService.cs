using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public class ReturnService
    {
        public sealed class ReturnSaleRow
        {
            public int SaleId { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public DateTime IssuedAt { get; set; }
            public string PaymentType { get; set; } = "";
            public double TotalUZS { get; set; }
        }

        public sealed class ReturnLineRow
        {
            public int SaleItemId { get; set; }
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public double SoldQty { get; set; }
            public double ReturnedQty { get; set; }
            public double AvailableQty { get; set; }
            public double UnitPriceUZS { get; set; }
            public double LineDiscountUZS { get; set; }
            public double PurchasePriceUZS { get; set; }
        }

        public sealed class ReturnApplyResult
        {
            public int ReturnId { get; set; }
            public double SubtotalUZS { get; set; }
            public double DiscountUZS { get; set; }
            public double TotalUZS { get; set; }
            public double ProfitReductionUZS { get; set; }
            public double DebtReducedUZS { get; set; }
        }

        public List<ReturnSaleRow> GetSales(DateTime from, DateTime to)
        {
            if (to < from)
            {
                (from, to) = (to, from);
            }

            DateTime fromBound = from.Date;
            DateTime toExclusive = to.Date.AddDays(1);
            string fromText = fromBound.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string toExclusiveText = toExclusive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var rows = new List<ReturnSaleRow>();

            using var connection = Database.GetConnection();
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT s.Id,
                       IFNULL(r.ReceiptNumber, 'CHK-' || printf('%06d', s.Id)),
                       IFNULL(r.IssuedAt, s.Date),
                       IFNULL(r.PaymentType, ''),
                       s.TotalUZS
                FROM Sales s
                LEFT JOIN SaleReceipts r ON r.SaleId = s.Id
                WHERE datetime(REPLACE(IFNULL(r.IssuedAt, s.Date), 'T', ' ')) >= datetime(@from)
                  AND datetime(REPLACE(IFNULL(r.IssuedAt, s.Date), 'T', ' ')) < datetime(@toExclusive)
                ORDER BY datetime(REPLACE(IFNULL(r.IssuedAt, s.Date), 'T', ' ')) DESC, s.Id DESC";
            cmd.Parameters.AddWithValue("@from", fromText);
            cmd.Parameters.AddWithValue("@toExclusive", toExclusiveText);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ReturnSaleRow
                {
                    SaleId = reader.GetInt32(0),
                    ReceiptNumber = reader.GetString(1),
                    IssuedAt = ParseDateTime(reader.GetString(2)),
                    PaymentType = reader.GetString(3),
                    TotalUZS = reader.GetDouble(4)
                });
            }

            return rows;
        }

        public List<ReturnLineRow> GetSaleLines(int saleId)
        {
            var rows = new List<ReturnLineRow>();

            using var connection = Database.GetConnection();
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT si.Id,
                       si.ProductId,
                       p.Name,
                       si.Quantity,
                       IFNULL((
                           SELECT SUM(ri.Quantity)
                           FROM ReturnItems ri
                           INNER JOIN Returns r ON r.Id = ri.ReturnId
                           WHERE ri.SaleItemId = si.Id AND r.SaleId = si.SaleId
                       ), 0) AS ReturnedQty,
                       si.SellPriceUZS,
                       IFNULL(si.DiscountUZS, 0),
                       IFNULL(p.PurchasePriceUZS, 0)
                FROM SaleItems si
                INNER JOIN Products p ON p.Id = si.ProductId
                WHERE si.SaleId = @saleId
                ORDER BY si.Id";
            cmd.Parameters.AddWithValue("@saleId", saleId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                double sold = reader.GetDouble(3);
                double returned = reader.GetDouble(4);
                double available = Math.Max(0, sold - returned);
                rows.Add(new ReturnLineRow
                {
                    SaleItemId = reader.GetInt32(0),
                    ProductId = reader.GetInt32(1),
                    ProductName = reader.GetString(2),
                    SoldQty = sold,
                    ReturnedQty = returned,
                    AvailableQty = available,
                    UnitPriceUZS = reader.GetDouble(5),
                    LineDiscountUZS = reader.GetDouble(6),
                    PurchasePriceUZS = reader.GetDouble(7)
                });
            }

            return rows;
        }

        public ReturnApplyResult ApplyReturn(
            int saleId,
            List<(int saleItemId, double quantity)> lines,
            string reason,
            AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanCreateSales(currentUser),
                "Qaytarib olish huquqi mavjud emas.");

            if (lines == null || lines.Count == 0)
            {
                throw new Exception("Qaytarish uchun kamida bitta mahsulot kiriting.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                var lineMap = lines
                    .Where(x => x.quantity > 0)
                    .GroupBy(x => x.saleItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.quantity));
                if (lineMap.Count == 0)
                {
                    throw new Exception("Qaytarish miqdori 0 dan katta bo'lishi kerak.");
                }

                var saleRead = connection.CreateCommand();
                saleRead.Transaction = tx;
                saleRead.CommandText = @"
                    SELECT TotalUZS, IFNULL(SubtotalUZS, TotalUZS), IFNULL(DiscountUZS, 0), IFNULL(ProfitUZS, 0)
                    FROM Sales
                    WHERE Id = @id
                    LIMIT 1";
                saleRead.Parameters.AddWithValue("@id", saleId);

                double oldTotal;
                double oldSubtotal;
                double oldDiscount;
                double oldProfit;
                using (var reader = saleRead.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new Exception("Sotuv topilmadi.");
                    }

                    oldTotal = reader.GetDouble(0);
                    oldSubtotal = reader.GetDouble(1);
                    oldDiscount = reader.GetDouble(2);
                    oldProfit = reader.GetDouble(3);
                }

                var saleItems = new Dictionary<int, ReturnLineRow>();
                var linesRead = connection.CreateCommand();
                linesRead.Transaction = tx;
                linesRead.CommandText = @"
                    SELECT si.Id,
                           si.ProductId,
                           p.Name,
                           si.Quantity,
                           IFNULL((
                               SELECT SUM(ri.Quantity)
                               FROM ReturnItems ri
                               INNER JOIN Returns r ON r.Id = ri.ReturnId
                               WHERE ri.SaleItemId = si.Id AND r.SaleId = si.SaleId
                           ), 0) AS ReturnedQty,
                           si.SellPriceUZS,
                           IFNULL(si.DiscountUZS, 0),
                           IFNULL(p.PurchasePriceUZS, 0)
                    FROM SaleItems si
                    INNER JOIN Products p ON p.Id = si.ProductId
                    WHERE si.SaleId = @saleId";
                linesRead.Parameters.AddWithValue("@saleId", saleId);
                using (var reader = linesRead.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        double sold = reader.GetDouble(3);
                        double returned = reader.GetDouble(4);
                        double available = Math.Max(0, sold - returned);
                        var row = new ReturnLineRow
                        {
                            SaleItemId = reader.GetInt32(0),
                            ProductId = reader.GetInt32(1),
                            ProductName = reader.GetString(2),
                            SoldQty = sold,
                            ReturnedQty = returned,
                            AvailableQty = available,
                            UnitPriceUZS = reader.GetDouble(5),
                            LineDiscountUZS = reader.GetDouble(6),
                            PurchasePriceUZS = reader.GetDouble(7)
                        };
                        saleItems[row.SaleItemId] = row;
                    }
                }

                double returnSubtotal = 0;
                double returnDiscount = 0;
                double returnTotal = 0;
                double profitReduction = 0;

                foreach ((int saleItemId, double qty) in lineMap.Select(x => (x.Key, x.Value)))
                {
                    if (!saleItems.TryGetValue(saleItemId, out ReturnLineRow? line))
                    {
                        throw new Exception($"Sotuv qatori topilmadi (Id={saleItemId}).");
                    }

                    if (qty <= 0)
                    {
                        continue;
                    }

                    if (qty > line.AvailableQty + 0.000001)
                    {
                        throw new Exception($"\"{line.ProductName}\" bo'yicha qaytarish miqdori katta. Maksimal: {line.AvailableQty:0.##}");
                    }

                    double perUnitDiscount = line.SoldQty > 0 ? line.LineDiscountUZS / line.SoldQty : 0;
                    double lineDiscount = perUnitDiscount * qty;
                    double lineTotal = line.UnitPriceUZS * qty;
                    double lineSubtotal = lineTotal + lineDiscount;

                    returnSubtotal += lineSubtotal;
                    returnDiscount += lineDiscount;
                    returnTotal += lineTotal;
                    profitReduction += (line.UnitPriceUZS - line.PurchasePriceUZS) * qty;
                }

                if (returnTotal <= 0)
                {
                    throw new Exception("Qaytarish summasi 0 bo'lib qoldi.");
                }

                var insertHead = connection.CreateCommand();
                insertHead.Transaction = tx;
                insertHead.CommandText = @"
                    INSERT INTO Returns (SaleId, ReturnDate, Reason, SubtotalUZS, DiscountUZS, TotalUZS, ProfitReductionUZS, CreatedByUser)
                    VALUES (@saleId, @date, @reason, @subtotal, @discount, @total, @profitReduction, @user);
                    SELECT last_insert_rowid();";
                insertHead.Parameters.AddWithValue("@saleId", saleId);
                insertHead.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                insertHead.Parameters.AddWithValue("@reason", string.IsNullOrWhiteSpace(reason) ? "" : reason.Trim());
                insertHead.Parameters.AddWithValue("@subtotal", returnSubtotal);
                insertHead.Parameters.AddWithValue("@discount", returnDiscount);
                insertHead.Parameters.AddWithValue("@total", returnTotal);
                insertHead.Parameters.AddWithValue("@profitReduction", Math.Max(0, profitReduction));
                insertHead.Parameters.AddWithValue("@user", currentUser.Username ?? string.Empty);
                int returnId = Convert.ToInt32(insertHead.ExecuteScalar(), CultureInfo.InvariantCulture);

                foreach ((int saleItemId, double qty) in lineMap.Select(x => (x.Key, x.Value)))
                {
                    if (!saleItems.TryGetValue(saleItemId, out ReturnLineRow? line) || qty <= 0)
                    {
                        continue;
                    }

                    double perUnitDiscount = line.SoldQty > 0 ? line.LineDiscountUZS / line.SoldQty : 0;
                    double lineDiscount = perUnitDiscount * qty;
                    double lineTotal = line.UnitPriceUZS * qty;

                    var insertLine = connection.CreateCommand();
                    insertLine.Transaction = tx;
                    insertLine.CommandText = @"
                        INSERT INTO ReturnItems (ReturnId, SaleItemId, ProductId, Quantity, UnitPriceUZS, DiscountUZS, LineTotalUZS)
                        VALUES (@returnId, @saleItemId, @productId, @qty, @unitPrice, @discount, @lineTotal)";
                    insertLine.Parameters.AddWithValue("@returnId", returnId);
                    insertLine.Parameters.AddWithValue("@saleItemId", saleItemId);
                    insertLine.Parameters.AddWithValue("@productId", line.ProductId);
                    insertLine.Parameters.AddWithValue("@qty", qty);
                    insertLine.Parameters.AddWithValue("@unitPrice", line.UnitPriceUZS);
                    insertLine.Parameters.AddWithValue("@discount", lineDiscount);
                    insertLine.Parameters.AddWithValue("@lineTotal", lineTotal);
                    insertLine.ExecuteNonQuery();

                    var stockUpdate = connection.CreateCommand();
                    stockUpdate.Transaction = tx;
                    stockUpdate.CommandText = "UPDATE Products SET QuantityUSD = QuantityUSD + @qty WHERE Id = @productId";
                    stockUpdate.Parameters.AddWithValue("@qty", qty);
                    stockUpdate.Parameters.AddWithValue("@productId", line.ProductId);
                    stockUpdate.ExecuteNonQuery();
                }

                double newSubtotal = Math.Max(0, oldSubtotal - returnSubtotal);
                double newDiscount = Math.Max(0, oldDiscount - returnDiscount);
                double newTotal = Math.Max(0, oldTotal - returnTotal);
                double newProfit = oldProfit - profitReduction;

                var saleUpdate = connection.CreateCommand();
                saleUpdate.Transaction = tx;
                saleUpdate.CommandText = @"
                    UPDATE Sales
                    SET SubtotalUZS = @subtotal,
                        DiscountUZS = @discount,
                        TotalUZS = @total,
                        ProfitUZS = @profit
                    WHERE Id = @saleId";
                saleUpdate.Parameters.AddWithValue("@subtotal", newSubtotal);
                saleUpdate.Parameters.AddWithValue("@discount", newDiscount);
                saleUpdate.Parameters.AddWithValue("@total", newTotal);
                saleUpdate.Parameters.AddWithValue("@profit", newProfit);
                saleUpdate.Parameters.AddWithValue("@saleId", saleId);
                saleUpdate.ExecuteNonQuery();

                double debtReduced = 0;
                var debtRead = connection.CreateCommand();
                debtRead.Transaction = tx;
                debtRead.CommandText = @"
                    SELECT Id, TotalAmountUZS, PaidAmountUZS, RemainingAmountUZS, DueDate
                    FROM Debts
                    WHERE SaleId = @saleId
                    LIMIT 1";
                debtRead.Parameters.AddWithValue("@saleId", saleId);
                using (var reader = debtRead.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int debtId = reader.GetInt32(0);
                        double debtTotal = reader.GetDouble(1);
                        double debtPaid = reader.GetDouble(2);
                        double debtRemaining = reader.GetDouble(3);
                        DateTime dueDate = ParseDate(reader.GetString(4));

                        double reduced = Math.Min(returnTotal, debtTotal);
                        double updatedTotal = Math.Max(0, debtTotal - reduced);
                        double updatedRemaining = Math.Max(0, debtRemaining - reduced);
                        double updatedPaid = Math.Max(0, updatedTotal - updatedRemaining);
                        string status = BuildDebtStatus(updatedRemaining, dueDate);

                        var debtUpdate = connection.CreateCommand();
                        debtUpdate.Transaction = tx;
                        debtUpdate.CommandText = @"
                            UPDATE Debts
                            SET TotalAmountUZS=@total,
                                PaidAmountUZS=@paid,
                                RemainingAmountUZS=@remaining,
                                Status=@status
                            WHERE Id=@id";
                        debtUpdate.Parameters.AddWithValue("@total", updatedTotal);
                        debtUpdate.Parameters.AddWithValue("@paid", updatedPaid);
                        debtUpdate.Parameters.AddWithValue("@remaining", updatedRemaining);
                        debtUpdate.Parameters.AddWithValue("@status", status);
                        debtUpdate.Parameters.AddWithValue("@id", debtId);
                        debtUpdate.ExecuteNonQuery();

                        if (debtPaid > updatedPaid + 0.000001)
                        {
                            var debtPaymentLog = connection.CreateCommand();
                            debtPaymentLog.Transaction = tx;
                            debtPaymentLog.CommandText = @"
                                INSERT INTO DebtPayments (DebtId, AmountUZS, PaymentType, Comment, PaymentDate)
                                VALUES (@debtId, @amount, @type, @comment, @date)";
                            debtPaymentLog.Parameters.AddWithValue("@debtId", debtId);
                            debtPaymentLog.Parameters.AddWithValue("@amount", debtPaid - updatedPaid);
                            debtPaymentLog.Parameters.AddWithValue("@type", "Qaytarish");
                            debtPaymentLog.Parameters.AddWithValue("@comment", $"Sotuv #{saleId} bo'yicha qaytarishda balans qayta hisoblandi");
                            debtPaymentLog.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                            debtPaymentLog.ExecuteNonQuery();
                        }

                        debtReduced = reduced;
                    }
                }

                tx.Commit();

                return new ReturnApplyResult
                {
                    ReturnId = returnId,
                    SubtotalUZS = returnSubtotal,
                    DiscountUZS = returnDiscount,
                    TotalUZS = returnTotal,
                    ProfitReductionUZS = profitReduction,
                    DebtReducedUZS = debtReduced
                };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static string BuildDebtStatus(double remaining, DateTime dueDate)
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
