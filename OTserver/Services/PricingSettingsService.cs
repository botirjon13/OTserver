using System;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using Microsoft.Data.Sqlite;

namespace SantexnikaSRM.Services
{
    public class PricingSettingsService
    {
        public (double SuggestedMarkupPercent, bool AutoFillSuggestedPrice, bool QuickDiscountEnabled) Get(AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanCreateSales(currentUser) || AuthorizationService.CanManagePricing(currentUser),
                "Foiz va chegirmalar sozlamasini o'qishga ruxsat yo'q.");

            using var connection = Database.GetConnection();
            connection.Open();
            EnsureQuickDiscountColumn(connection);
            bool hasQuickDiscountColumn = HasColumn(connection, "PricingSettings", "QuickDiscountEnabled");
            using var cmd = connection.CreateCommand();
            cmd.CommandText = hasQuickDiscountColumn
                ? @"
                    SELECT SuggestedMarkupPercent, AutoFillSuggestedPrice, IFNULL(QuickDiscountEnabled, 1)
                    FROM PricingSettings
                    WHERE Id = 1
                    LIMIT 1"
                : @"
                    SELECT SuggestedMarkupPercent, AutoFillSuggestedPrice
                    FROM PricingSettings
                    WHERE Id = 1
                    LIMIT 1";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return (20, true, true);
            }

            double markup = reader.GetDouble(0);
            bool autoFill = !reader.IsDBNull(1) && reader.GetInt32(1) == 1;
            bool quickDiscountEnabled = hasQuickDiscountColumn
                ? (!reader.IsDBNull(2) && reader.GetInt32(2) == 1)
                : true;

            return (markup, autoFill, quickDiscountEnabled);
        }

        public void Save(double suggestedMarkupPercent, bool autoFillSuggestedPrice, bool quickDiscountEnabled, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManagePricing(currentUser),
                "Foiz va chegirmalarni o'zgartirish huquqi yo'q.");

            if (suggestedMarkupPercent < 0 || suggestedMarkupPercent > 1000)
            {
                throw new Exception("Foiz 0 dan 1000 gacha bo'lishi kerak.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            EnsureQuickDiscountColumn(connection);
            bool hasQuickDiscountColumn = HasColumn(connection, "PricingSettings", "QuickDiscountEnabled");
            using var cmd = connection.CreateCommand();
            cmd.CommandText = hasQuickDiscountColumn
                ? @"
                    INSERT INTO PricingSettings (Id, SuggestedMarkupPercent, AutoFillSuggestedPrice, QuickDiscountEnabled)
                    VALUES (1, @markup, @autoFill, @quickDiscount)
                    ON CONFLICT(Id) DO UPDATE SET
                        SuggestedMarkupPercent = excluded.SuggestedMarkupPercent,
                        AutoFillSuggestedPrice = excluded.AutoFillSuggestedPrice,
                        QuickDiscountEnabled = excluded.QuickDiscountEnabled"
                : @"
                    INSERT INTO PricingSettings (Id, SuggestedMarkupPercent, AutoFillSuggestedPrice)
                    VALUES (1, @markup, @autoFill)
                    ON CONFLICT(Id) DO UPDATE SET
                        SuggestedMarkupPercent = excluded.SuggestedMarkupPercent,
                        AutoFillSuggestedPrice = excluded.AutoFillSuggestedPrice";
            cmd.Parameters.AddWithValue("@markup", suggestedMarkupPercent);
            cmd.Parameters.AddWithValue("@autoFill", autoFillSuggestedPrice ? 1 : 0);
            if (hasQuickDiscountColumn)
            {
                cmd.Parameters.AddWithValue("@quickDiscount", quickDiscountEnabled ? 1 : 0);
            }
            cmd.ExecuteNonQuery();
        }

        private static void EnsureQuickDiscountColumn(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE PricingSettings ADD COLUMN QuickDiscountEnabled INTEGER NOT NULL DEFAULT 1;";
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Column oldindan mavjud bo'lsa yoki legacy sxemada alter qo'llab bo'lmasa e'tiborsiz qoldiriladi.
            }
        }

        private static bool HasColumn(SqliteConnection connection, string table, string column)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
