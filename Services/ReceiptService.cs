using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public class ReceiptService
    {
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
                SELECT p.Name, si.Quantity, si.SellPriceUZS
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
                        ProductName = reader.GetString(0),
                        Quantity = reader.GetDouble(1),
                        UnitPriceUZS = reader.GetDouble(2)
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
            string fiscalSign = BuildFiscalSign(saleId, total, issuedAt);
            string qrData = BuildQrData(settings, receiptNumber, saleId, total, fiscalSign, issuedAt);

            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR REPLACE INTO SaleReceipts
                (SaleId, ReceiptNumber, IssuedAt, PaymentType, FiscalSign, QrData, BusinessName, TIN, StoreAddress, KkmNumber, IsVatPayer, VatRatePercent, VatAmount, TotalUZS)
                VALUES
                (@saleId, @rn, @issued, @pay, @fs, @qr, @b, @tin, @a, @kkm, @vat, @vr, @va, @t)";
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
            insert.Parameters.AddWithValue("@vat", settings.IsVatPayer ? 1 : 0);
            insert.Parameters.AddWithValue("@vr", settings.VatRatePercent);
            insert.Parameters.AddWithValue("@va", vatAmount);
            insert.Parameters.AddWithValue("@t", total);
            insert.ExecuteNonQuery();

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

        private static string BuildFiscalSign(int saleId, double total, DateTime issuedAt)
        {
            string raw = $"{saleId}|{total:F2}|{issuedAt:yyyyMMddHHmmss}";
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes)[..12];
        }

        private static string BuildQrData(FiscalSettings settings, string receiptNumber, int saleId, double total, string fiscalSign, DateTime issuedAt)
        {
            return
                $"receipt={receiptNumber};sale={saleId};time={issuedAt:yyyy-MM-dd HH:mm:ss};" +
                $"sum={total:N0};tin={settings.TIN};kkm={settings.KkmNumber};fiscal={fiscalSign}";
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
