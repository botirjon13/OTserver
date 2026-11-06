using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public class ReceiptService
    {
        public sealed class ReceiptHistoryItem
        {
            public int SaleId { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public DateTime IssuedAt { get; set; }
            public string PaymentType { get; set; } = "";
            public double TotalUZS { get; set; }
        }

        public SaleReceipt CreateAndSave(int saleId, string paymentType, AppUser currentUser)
        {
            using var connection = Database.GetConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            SaleReceipt receipt = CreateAndSave(connection, tx, saleId, paymentType, currentUser);
            tx.Commit();
            return receipt;
        }

        public SaleReceipt CreateAndSave(
            SqliteConnection connection,
            SqliteTransaction transaction,
            int saleId,
            string paymentType,
            AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanCreateSales(currentUser),
                "Chek yaratish huquqi mavjud emas.");

            var saleCmd = connection.CreateCommand();
            saleCmd.Transaction = transaction;
            saleCmd.CommandText = @"
                SELECT Date, TotalUZS,
                       IFNULL(SubtotalUZS, TotalUZS),
                       IFNULL(DiscountUZS, 0),
                       IFNULL(DiscountType, 'None'),
                       IFNULL(DiscountValue, 0)
                FROM Sales
                WHERE Id=@id
                LIMIT 1";
            saleCmd.Parameters.AddWithValue("@id", saleId);

            DateTime issuedAt;
            double total;
            double subtotal;
            double discount;
            string discountType;
            double discountValue;
            using (var reader = saleCmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    throw new Exception("Sotuv topilmadi.");
                }

                issuedAt = ParseDateTime(reader.GetString(0));
                total = reader.GetDouble(1);
                subtotal = reader.GetDouble(2);
                discount = reader.GetDouble(3);
                discountType = reader.GetString(4);
                discountValue = reader.GetDouble(5);
            }

            var items = new List<SaleReceiptItem>();
            var itemCmd = connection.CreateCommand();
            itemCmd.Transaction = transaction;
            itemCmd.CommandText = @"
                SELECT si.Id, p.Name, si.Quantity, si.SellPriceUZS
                FROM SaleItems si
                INNER JOIN Products p ON p.Id = si.ProductId
                WHERE si.SaleId = @saleId";
            itemCmd.Parameters.AddWithValue("@saleId", saleId);
            using (var reader = itemCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    items.Add(new SaleReceiptItem
                    {
                        SaleItemId = reader.GetInt32(0),
                        ProductName = reader.GetString(1),
                        Quantity = reader.GetDouble(2),
                        UnitPriceUZS = reader.GetDouble(3)
                    });
                }
            }

            var settingsService = new FiscalSettingsService();
            FiscalSettings settings = settingsService.Get(currentUser);

            double vatAmount = 0;
            if (settings.IsVatPayer && settings.VatRatePercent > 0)
            {
                vatAmount = total * settings.VatRatePercent / (100.0 + settings.VatRatePercent);
            }

            string receiptNumber = $"CHK-{saleId:D6}";
            const string fiscalSign = "";
            const string qrData = "";
            byte[] logoPng = settings.LogoPng ?? Array.Empty<byte>();

            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR REPLACE INTO SaleReceipts
                (SaleId, ReceiptNumber, IssuedAt, PaymentType, FiscalSign, QrData, BusinessName, TIN, StoreAddress, KkmNumber, StorePhone, ThankYouMessage, SellerName, LogoPng, IsVatPayer, VatRatePercent, VatAmount, SubtotalUZS, DiscountUZS, DiscountType, DiscountValue, TotalUZS)
                VALUES
                (@saleId, @rn, @issued, @pay, @fs, @qr, @b, @tin, @a, @kkm, @phone, @thanks, @seller, @logo, @vat, @vr, @va, @subtotal, @discount, @discountType, @discountValue, @t)";
            insert.Parameters.AddWithValue("@saleId", saleId);
            insert.Parameters.AddWithValue("@rn", receiptNumber);
            insert.Parameters.AddWithValue("@issued", issuedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            insert.Parameters.AddWithValue("@pay", paymentType);
            insert.Parameters.AddWithValue("@fs", fiscalSign);
            insert.Parameters.AddWithValue("@qr", qrData);
            insert.Parameters.AddWithValue("@b", settings.BusinessName);
            insert.Parameters.AddWithValue("@tin", settings.TIN);
            insert.Parameters.AddWithValue("@a", settings.StoreAddress);
            insert.Parameters.AddWithValue("@kkm", settings.KkmNumber);
            insert.Parameters.AddWithValue("@phone", settings.StorePhone);
            insert.Parameters.AddWithValue("@thanks", settings.ThankYouMessage);
            insert.Parameters.AddWithValue("@seller", currentUser.Username?.Trim() ?? "");
            insert.Parameters.AddWithValue("@logo", logoPng.Length == 0 ? DBNull.Value : logoPng);
            insert.Parameters.AddWithValue("@vat", settings.IsVatPayer ? 1 : 0);
            insert.Parameters.AddWithValue("@vr", settings.VatRatePercent);
            insert.Parameters.AddWithValue("@va", vatAmount);
            insert.Parameters.AddWithValue("@subtotal", subtotal);
            insert.Parameters.AddWithValue("@discount", discount);
            insert.Parameters.AddWithValue("@discountType", discountType);
            insert.Parameters.AddWithValue("@discountValue", discountValue);
            insert.Parameters.AddWithValue("@t", total);
            insert.ExecuteNonQuery();

            foreach (SaleReceiptItem item in items)
            {
                var itemInsert = connection.CreateCommand();
                itemInsert.Transaction = transaction;
                itemInsert.CommandText = @"
                    INSERT INTO SaleReceiptItems (SaleItemId, SaleId, ProductName, Quantity, UnitPriceUZS)
                    VALUES (@saleItemId, @saleId, @name, @quantity, @unitPrice)";
                itemInsert.Parameters.AddWithValue("@saleItemId", item.SaleItemId);
                itemInsert.Parameters.AddWithValue("@saleId", saleId);
                itemInsert.Parameters.AddWithValue("@name", item.ProductName);
                itemInsert.Parameters.AddWithValue("@quantity", item.Quantity);
                itemInsert.Parameters.AddWithValue("@unitPrice", item.UnitPriceUZS);
                itemInsert.ExecuteNonQuery();
            }

            return new SaleReceipt
            {
                SaleId = saleId,
                ReceiptNumber = receiptNumber,
                IssuedAt = issuedAt,
                PaymentType = paymentType,
                FiscalSign = fiscalSign,
                QrData = qrData,
                BusinessName = settings.BusinessName,
                TIN = settings.TIN,
                StoreAddress = settings.StoreAddress,
                KkmNumber = settings.KkmNumber,
                StorePhone = settings.StorePhone,
                ThankYouMessage = settings.ThankYouMessage,
                SellerName = currentUser.Username?.Trim() ?? "",
                LogoPng = logoPng,
                IsVatPayer = settings.IsVatPayer,
                VatRatePercent = settings.VatRatePercent,
                VatAmount = vatAmount,
                SubtotalUZS = subtotal,
                DiscountUZS = discount,
                DiscountType = discountType,
                DiscountValue = discountValue,
                TotalUZS = total,
                Items = items
            };
        }

        public List<ReceiptHistoryItem> GetHistory(DateTime from, DateTime to)
        {
            if (to < from)
            {
                (from, to) = (to, from);
            }

            DateTime fromBound = from.Date;
            DateTime toExclusive = to.Date.AddDays(1);
            string fromText = fromBound.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string toExclusiveText = toExclusive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var list = new List<ReceiptHistoryItem>();

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
                       IssuedAtNorm AS IssuedAt,
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
                list.Add(new ReceiptHistoryItem
                {
                    SaleId = reader.GetInt32(0),
                    ReceiptNumber = reader.GetString(1),
                    IssuedAt = ParseDateTime(reader.GetString(2)),
                    PaymentType = reader.GetString(3),
                    TotalUZS = reader.GetDouble(4)
                });
            }

            return list;
        }

        public SaleReceipt? GetBySaleId(int saleId)
        {
            using var connection = Database.GetConnection();
            connection.Open();

            var headCmd = connection.CreateCommand();
            headCmd.CommandText = @"
                SELECT s.Id,
                       s.Date,
                       COALESCE(r.TotalUZS, s.TotalUZS),
                       COALESCE(NULLIF(r.SubtotalUZS, 0), s.SubtotalUZS, s.TotalUZS),
                       COALESCE(r.DiscountUZS, s.DiscountUZS, 0),
                       COALESCE(r.DiscountType, s.DiscountType, 'None'),
                       COALESCE(r.DiscountValue, s.DiscountValue, 0),
                       IFNULL(r.ReceiptNumber, ''),
                       IFNULL(r.IssuedAt, ''),
                       IFNULL(r.PaymentType, ''),
                       IFNULL(r.FiscalSign, ''),
                       IFNULL(r.QrData, ''),
                       IFNULL(r.BusinessName, ''),
                       IFNULL(r.TIN, ''),
                       IFNULL(r.StoreAddress, ''),
                       IFNULL(r.KkmNumber, ''),
                       IFNULL(r.StorePhone, ''),
                       IFNULL(r.ThankYouMessage, ''),
                       IFNULL(r.SellerName, ''),
                       r.LogoPng,
                       IFNULL(r.IsVatPayer, 0),
                       IFNULL(r.VatRatePercent, 0),
                       IFNULL(r.VatAmount, 0)
                FROM Sales s
                LEFT JOIN SaleReceipts r ON r.SaleId = s.Id
                WHERE s.Id = @saleId
                LIMIT 1";
            headCmd.Parameters.AddWithValue("@saleId", saleId);

            using var reader = headCmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            DateTime saleDate = ParseDateTime(reader.GetString(1));
            string receiptNumber = reader.GetString(7);
            string receiptIssuedRaw = reader.GetString(8);

            var result = new SaleReceipt
            {
                SaleId = reader.GetInt32(0),
                ReceiptNumber = string.IsNullOrWhiteSpace(receiptNumber) ? $"CHK-{saleId:D6}" : receiptNumber,
                IssuedAt = string.IsNullOrWhiteSpace(receiptIssuedRaw) ? saleDate : ParseDateTime(receiptIssuedRaw),
                PaymentType = reader.GetString(9),
                FiscalSign = reader.GetString(10),
                QrData = reader.GetString(11),
                BusinessName = reader.GetString(12),
                TIN = reader.GetString(13),
                StoreAddress = reader.GetString(14),
                KkmNumber = reader.GetString(15),
                StorePhone = reader.GetString(16),
                ThankYouMessage = reader.GetString(17),
                SellerName = reader.GetString(18),
                LogoPng = reader.IsDBNull(19) ? Array.Empty<byte>() : (byte[])reader[19],
                IsVatPayer = reader.GetInt32(20) == 1,
                VatRatePercent = reader.GetDouble(21),
                VatAmount = reader.GetDouble(22),
                SubtotalUZS = reader.GetDouble(3),
                DiscountUZS = reader.GetDouble(4),
                DiscountType = reader.GetString(5),
                DiscountValue = reader.GetDouble(6),
                TotalUZS = reader.GetDouble(2)
            };

            var itemCmd = connection.CreateCommand();
            itemCmd.CommandText = @"
                SELECT COALESCE(rsi.ProductName, p.Name),
                       si.Id,
                       COALESCE(rsi.Quantity, si.Quantity),
                       COALESCE(rsi.UnitPriceUZS, si.SellPriceUZS)
                FROM SaleItems si
                INNER JOIN Products p ON p.Id = si.ProductId
                LEFT JOIN SaleReceiptItems rsi ON rsi.SaleItemId = si.Id AND rsi.SaleId = si.SaleId
                WHERE si.SaleId = @saleId";
            itemCmd.Parameters.AddWithValue("@saleId", saleId);
            using var itemReader = itemCmd.ExecuteReader();
            while (itemReader.Read())
            {
                result.Items.Add(new SaleReceiptItem
                {
                    ProductName = itemReader.GetString(0),
                    SaleItemId = itemReader.GetInt32(1),
                    Quantity = itemReader.GetDouble(2),
                    UnitPriceUZS = itemReader.GetDouble(3)
                });
            }

            return result;
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
    }
}
