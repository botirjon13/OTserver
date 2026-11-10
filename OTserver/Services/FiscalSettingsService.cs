using System;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public class FiscalSettingsService
    {
        public FiscalSettings Get(AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanCreateSales(currentUser) || AuthorizationService.CanManageBackups(currentUser) || AuthorizationService.CanViewReports(currentUser),
                "Chek sozlamalarini ko'rish huquqi mavjud emas.");

            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT Id, BusinessName, TIN, StoreAddress, KkmNumber, IsVatPayer, VatRatePercent, StorePhone, ThankYouMessage, LogoPng FROM FiscalSettings WHERE Id = 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new FiscalSettings { Id = 1 };
            }

            return new FiscalSettings
            {
                Id = reader.GetInt32(0),
                BusinessName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                TIN = reader.IsDBNull(2) ? "" : reader.GetString(2),
                StoreAddress = reader.IsDBNull(3) ? "" : reader.GetString(3),
                KkmNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                IsVatPayer = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                VatRatePercent = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                StorePhone = reader.IsDBNull(7) ? "" : reader.GetString(7),
                ThankYouMessage = reader.IsDBNull(8) ? "" : reader.GetString(8),
                LogoPng = reader.IsDBNull(9) ? Array.Empty<byte>() : (byte[])reader[9]
            };
        }

        public void Save(FiscalSettings settings, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageBackups(currentUser),
                "Chek sozlamalarini o'zgartirish huquqi mavjud emas.");

            if (settings.VatRatePercent < 0 || settings.VatRatePercent > 100)
            {
                throw new Exception("QQS foizi 0 va 100 oralig'ida bo'lishi kerak.");
            }

            if ((settings.LogoPng?.Length ?? 0) > 1_048_576)
            {
                throw new Exception("Logotip PNG fayli 1 MB dan katta bo'lmasligi kerak.");
            }
            if ((settings.StorePhone?.Trim().Length ?? 0) > 64)
            {
                throw new Exception("Do'kon telefon raqami 64 ta belgidan oshmasligi kerak.");
            }
            if ((settings.ThankYouMessage?.Trim().Length ?? 0) > 500)
            {
                throw new Exception("Rahmatnoma matni 500 ta belgidan oshmasligi kerak.");
            }

            using var connection = Database.GetConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE FiscalSettings
                SET BusinessName = @b, TIN = @tin, StoreAddress = @a, KkmNumber = @k,
                    StorePhone = @phone, ThankYouMessage = @thanks, LogoPng = @logo,
                    IsVatPayer = @v, VatRatePercent = @vr
                WHERE Id = 1";
            cmd.Parameters.AddWithValue("@b", settings.BusinessName?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@tin", settings.TIN?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@a", settings.StoreAddress?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@k", settings.KkmNumber?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@phone", settings.StorePhone?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@thanks", settings.ThankYouMessage?.Trim() ?? "");
            byte[] logoPng = settings.LogoPng ?? Array.Empty<byte>();
            cmd.Parameters.AddWithValue("@logo", logoPng.Length == 0 ? DBNull.Value : logoPng);
            cmd.Parameters.AddWithValue("@v", settings.IsVatPayer ? 1 : 0);
            cmd.Parameters.AddWithValue("@vr", settings.VatRatePercent);
            cmd.ExecuteNonQuery();
        }
    }
}
