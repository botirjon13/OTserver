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

        public sealed class ReturnHistoryItem
        {
            public int ReturnId { get; set; }
            public int SaleId { get; set; }
            public string OriginalReceiptNumber { get; set; } = "";
            public DateTime IssuedAt { get; set; }
            public double TotalUZS { get; set; }
        }

        public sealed class ReturnApplyResult
        {
            public int ReturnId { get; set; }
            public double SubtotalUZS { get; set; }
            public double DiscountUZS { get; set; }
            public double TotalUZS { get; set; }
            public double ProfitReductionUZS { get; set; }
            public double DebtReducedUZS { get; set; }
            public double CashRefundUZS { get; set; }
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
                WITH base AS (
                    SELECT s.Id AS SaleId,
                           IFNULL(r.ReceiptNumber, 'CHK-' || printf('%06d', s.Id)) AS ReceiptNumber,
                           IFNULL(r.IssuedAt, s.Date) AS RawIssuedAt,
                           IFNULL(r.PaymentType, '') AS PaymentType,
                           s.TotalUZS
                    FROM Sales s
                    LEFT JOIN SaleReceipts r ON r.SaleId = s.Id
                ),
                norm AS (
                    SELECT SaleId,
                           ReceiptNumber,
                           RawIssuedAt,
                           PaymentType,
                           TotalUZS,
                           CASE
                               WHEN RawIssuedAt LIKE '__.__.____%' THEN
                                   substr(RawIssuedAt, 7, 4) || '-' || substr(RawIssuedAt, 4, 2) || '-' || substr(RawIssuedAt, 1, 2) || substr(RawIssuedAt, 11)
                               ELSE
                                   REPLACE(REPLACE(RawIssuedAt, 'T', ' '), 'Z', '')
                           END AS IssuedAtNorm,
                           CASE
                               WHEN RawIssuedAt LIKE '__.__.____%' THEN
                                   substr(RawIssuedAt, 7, 4) || '-' || substr(RawIssuedAt, 4, 2) || '-' || substr(RawIssuedAt, 1, 2)
                               WHEN RawIssuedAt LIKE '____-__-__%' THEN
                                   substr(RawIssuedAt, 1, 10)
                               ELSE
                                   date(REPLACE(REPLACE(RawIssuedAt, 'T', ' '), 'Z', ''))
                           END AS IssuedDateIso
                    FROM base
                )
                SELECT SaleId,
                       ReceiptNumber,
                       IssuedAtNorm,
                       PaymentType,
                       TotalUZS
                FROM norm
                WHERE IssuedDateIso IS NOT NULL
                  AND IssuedDateIso >= date(@from)
                  AND IssuedDateIso < date(@toExclusive)
                ORDER BY IssuedDateIso DESC, SaleId DESC";
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
                       COALESCE(rsi.ProductName, p.Name),
                       si.Quantity,
                       IFNULL((
                           SELECT SUM(ri.Quantity)
                           FROM ReturnItems ri
                           INNER JOIN Returns r ON r.Id = ri.ReturnId
                           WHERE ri.SaleItemId = si.Id AND r.SaleId = si.SaleId
                       ), 0) AS ReturnedQty,
                       si.SellPriceUZS,
                       IFNULL(si.DiscountUZS, 0),
                       CASE WHEN si.UnitCostUZS > 0 THEN si.UnitCostUZS ELSE IFNULL(p.PurchasePriceUZS, 0) END
                FROM SaleItems si
                INNER JOIN Products p ON p.Id = si.ProductId
                LEFT JOIN SaleReceiptItems rsi ON rsi.SaleItemId = si.Id AND rsi.SaleId = si.SaleId
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

        public List<ReturnHistoryItem> GetReturnHistory(DateTime from, DateTime to)
        {
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var rows = new List<ReturnHistoryItem>();
            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT r.Id,
                       r.SaleId,
                       IFNULL(sr.ReceiptNumber, 'CHK-' || printf('%06d', r.SaleId)),
                       r.ReturnDate,
                       r.TotalUZS
                FROM Returns r
                LEFT JOIN SaleReceipts sr ON sr.SaleId = r.SaleId
                WHERE r.ReturnDate >= @from
                  AND r.ReturnDate < @toExclusive
                ORDER BY r.ReturnDate DESC, r.Id DESC";
            cmd.Parameters.AddWithValue("@from", from.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@toExclusive", to.Date.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ReturnHistoryItem
                {
                    ReturnId = reader.GetInt32(0),
                    SaleId = reader.GetInt32(1),
                    OriginalReceiptNumber = reader.GetString(2),
                    IssuedAt = ParseDateTime(reader.GetString(3)),
                    TotalUZS = reader.GetDouble(4)
                });
            }

            return rows;
        }

        public ReturnReceipt? GetReturnReceipt(int returnId)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var headCmd = connection.CreateCommand();
            headCmd.CommandText = @"
                SELECT r.Id,
                       r.SaleId,
                       r.ReturnDate,
                       r.Reason,
                       r.SubtotalUZS,
                       r.DiscountUZS,
                       r.TotalUZS,
                       r.DebtReducedUZS,
                       r.CashRefundUZS,
                       IFNULL(sr.ReceiptNumber, 'CHK-' || printf('%06d', r.SaleId)),
                       IFNULL(sr.BusinessName, ''),
                       IFNULL(sr.TIN, ''),
                       IFNULL(sr.StoreAddress, ''),
                       IFNULL(sr.StorePhone, ''),
                       sr.LogoPng
                FROM Returns r
                LEFT JOIN SaleReceipts sr ON sr.SaleId = r.SaleId
                WHERE r.Id = @returnId
                LIMIT 1";
            headCmd.Parameters.AddWithValue("@returnId", returnId);

            ReturnReceipt result;
            using (var reader = headCmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                result = new ReturnReceipt
                {
                    ReturnId = reader.GetInt32(0),
                    SaleId = reader.GetInt32(1),
                    IssuedAt = ParseDateTime(reader.GetString(2)),
                    Reason = reader.GetString(3),
                    SubtotalUZS = reader.GetDouble(4),
                    DiscountUZS = reader.GetDouble(5),
                    TotalUZS = reader.GetDouble(6),
                    DebtReducedUZS = reader.GetDouble(7),
                    CashRefundUZS = reader.GetDouble(8),
                    OriginalReceiptNumber = reader.GetString(9),
                    BusinessName = reader.GetString(10),
                    TIN = reader.GetString(11),
                    StoreAddress = reader.GetString(12),
                    StorePhone = reader.GetString(13),
                    LogoPng = reader.IsDBNull(14) ? Array.Empty<byte>() : (byte[])reader[14]
                };
            }

            using var itemCmd = connection.CreateCommand();
            itemCmd.CommandText = @"
                SELECT COALESCE(NULLIF(ri.ProductName, ''), srsi.ProductName, p.Name),
                       ri.Quantity,
                       ri.UnitPriceUZS,
                       ri.LineTotalUZS
                FROM ReturnItems ri
                INNER JOIN Products p ON p.Id = ri.ProductId
                LEFT JOIN SaleReceiptItems srsi ON srsi.SaleItemId = ri.SaleItemId
                WHERE ri.ReturnId = @returnId
                ORDER BY ri.Id";
            itemCmd.Parameters.AddWithValue("@returnId", returnId);
            using var itemReader = itemCmd.ExecuteReader();
            while (itemReader.Read())
            {
                result.Items.Add(new ReturnReceiptItem
                {
                    ProductName = itemReader.GetString(0),
                    Quantity = itemReader.GetDouble(1),
                    UnitPriceUZS = itemReader.GetDouble(2),
                    LineTotalUZS = itemReader.GetDouble(3)
                });
            }

            return result;
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
                           COALESCE(rsi.ProductName, p.Name),
                           si.Quantity,
                           IFNULL((
                               SELECT SUM(ri.Quantity)
                               FROM ReturnItems ri
                               INNER JOIN Returns r ON r.Id = ri.ReturnId
                               WHERE ri.SaleItemId = si.Id AND r.SaleId = si.SaleId
                           ), 0) AS ReturnedQty,
                           si.SellPriceUZS,
                           IFNULL(si.DiscountUZS, 0),
                           CASE WHEN si.UnitCostUZS > 0 THEN si.UnitCostUZS ELSE IFNULL(p.PurchasePriceUZS, 0) END
                    FROM SaleItems si
                    INNER JOIN Products p ON p.Id = si.ProductId
                    LEFT JOIN SaleReceiptItems rsi ON rsi.SaleItemId = si.Id AND rsi.SaleId = si.SaleId
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

                    double netUnitPrice = line.UnitPriceUZS;
                    double perUnitDiscount = line.SoldQty > 0 ? line.LineDiscountUZS / line.SoldQty : 0;
                    double lineDiscount = perUnitDiscount * qty;
                    double lineNetTotal = netUnitPrice * qty;
                    double lineGrossSubtotal = lineNetTotal + lineDiscount;

                    returnSubtotal += lineGrossSubtotal;
                    returnDiscount += lineDiscount;
                    returnTotal += lineNetTotal;
                    profitReduction += (netUnitPrice - line.PurchasePriceUZS) * qty;
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
                insertHead.Parameters.AddWithValue("@profitReduction", profitReduction);
                insertHead.Parameters.AddWithValue("@user", currentUser.Username ?? string.Empty);
                int returnId = Convert.ToInt32(insertHead.ExecuteScalar(), CultureInfo.InvariantCulture);

                foreach ((int saleItemId, double qty) in lineMap.Select(x => (x.Key, x.Value)))
                {
                    if (!saleItems.TryGetValue(saleItemId, out ReturnLineRow? line) || qty <= 0)
                    {
                        continue;
                    }

                    double netUnitPrice = line.UnitPriceUZS;
                    double perUnitDiscount = line.SoldQty > 0 ? line.LineDiscountUZS / line.SoldQty : 0;
                    double lineDiscount = perUnitDiscount * qty;
                    double lineNetTotal = netUnitPrice * qty;

                    var insertLine = connection.CreateCommand();
                    insertLine.Transaction = tx;
                    insertLine.CommandText = @"
                        INSERT INTO ReturnItems (ReturnId, SaleItemId, ProductId, ProductName, Quantity, UnitPriceUZS, DiscountUZS, LineTotalUZS)
                        VALUES (@returnId, @saleItemId, @productId, @productName, @qty, @unitPrice, @discount, @lineTotal)";
                    insertLine.Parameters.AddWithValue("@returnId", returnId);
                    insertLine.Parameters.AddWithValue("@saleItemId", saleItemId);
                    insertLine.Parameters.AddWithValue("@productId", line.ProductId);
                    insertLine.Parameters.AddWithValue("@productName", line.ProductName);
                    insertLine.Parameters.AddWithValue("@qty", qty);
                    insertLine.Parameters.AddWithValue("@unitPrice", netUnitPrice);
                    insertLine.Parameters.AddWithValue("@discount", lineDiscount);
                    insertLine.Parameters.AddWithValue("@lineTotal", lineNetTotal);
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

                        double totalReduction = Math.Min(returnTotal, debtTotal);
                        double reduced = Math.Min(returnTotal, debtRemaining);
                        double updatedTotal = Math.Max(0, debtTotal - totalReduction);
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

                double cashRefund = Math.Max(0, returnTotal - debtReduced);
                var returnSettlement = connection.CreateCommand();
                returnSettlement.Transaction = tx;
                returnSettlement.CommandText = "UPDATE Returns SET DebtReducedUZS=@debtReduced, CashRefundUZS=@cashRefund WHERE Id=@returnId";
                returnSettlement.Parameters.AddWithValue("@debtReduced", debtReduced);
                returnSettlement.Parameters.AddWithValue("@cashRefund", cashRefund);
                returnSettlement.Parameters.AddWithValue("@returnId", returnId);
                returnSettlement.ExecuteNonQuery();

                tx.Commit();

                return new ReturnApplyResult
                {
                    ReturnId = returnId,
                    SubtotalUZS = returnSubtotal,
                    DiscountUZS = returnDiscount,
                    TotalUZS = returnTotal,
                    ProfitReductionUZS = profitReduction,
                    DebtReducedUZS = debtReduced,
                    CashRefundUZS = cashRefund
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
            string value = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTime.MinValue;
            }

            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.fffffff",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                "dd.MM.yyyy HH:mm:ss",
                "dd.MM.yyyy HH:mm"
            };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
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
