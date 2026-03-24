using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using SantexnikaSRM.Models;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Utils
{
    public static class CurrencyHelper
    {
        private const string CbuApiUrl = "https://cbu.uz/uz/arkhiv-kursov-valyut/json/";

        private sealed class CbuRateItem
        {
            public string Ccy { get; set; } = string.Empty;
            public string Rate { get; set; } = string.Empty;
        }

        public static double ConvertUsdToUzs(double usd, double rate)
        {
            return usd * rate;
        }

        public static async Task<double> GetUsdRateFromApi()
        {
            try
            {
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await client.GetStringAsync(CbuApiUrl);
                var data = JsonConvert.DeserializeObject<List<CbuRateItem>>(response);
                var usdData = data?.FirstOrDefault(x => x.Ccy == "USD");

                if (!string.IsNullOrWhiteSpace(usdData?.Rate))
                {
                    string normalizedRate = usdData.Rate.Replace(",", ".");
                    if (double.TryParse(normalizedRate, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRate))
                    {
                        return parsedRate;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("API xatosi: " + ex.Message);
            }

            return 0;
        }

        public static async Task CheckAndUpdateRate(DatabaseHelper dbHelper)
        {
            var lastRate = dbHelper.GetLastCurrencyRate();

            // 24 soat o'tganini yoki bazada kurs yo'qligini tekshirish
            if (lastRate == null || (DateTime.Now - lastRate.Date).TotalHours >= 24)
            {
                double newRate = await GetUsdRateFromApi();

                if (newRate > 1000)
                {
                    var rateModel = new CurrencyRate
                    {
                        Rate = newRate,
                        Date = DateTime.Now
                    };
                    dbHelper.SaveCurrencyRate(rateModel);
                }
            }
        }
    }
}
