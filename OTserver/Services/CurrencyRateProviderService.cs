using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SantexnikaSRM.Services
{
    public sealed class CurrencyRateProviderService
    {
        private static readonly HttpClient Http = CreateHttpClient();

        public async Task<(double Rate, string Source)> FetchUsdToUzsRateAsync(CancellationToken cancellationToken = default)
        {
            var errors = new List<string>();

            try
            {
                double cbuRate = await FetchFromCbuAsync(cancellationToken);
                return (cbuRate, "CBU");
            }
            catch (Exception ex)
            {
                errors.Add(ToFriendlyMessage(ex));
            }

            try
            {
                double erRate = await FetchFromErApiAsync(cancellationToken);
                return (erRate, "ER-API");
            }
            catch (Exception ex)
            {
                errors.Add(ToFriendlyMessage(ex));
            }

            string reason = errors.Count > 0
                ? string.Join(" | ", errors)
                : "Kurs serverlariga ulanishda noma'lum xatolik.";
            throw new Exception(reason);
        }

        private static async Task<double> FetchFromCbuAsync(CancellationToken cancellationToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://cbu.uz/uz/arkhiv-kursov-valyut/json/USD/");
            using HttpResponseMessage resp = await Http.SendAsync(req, cancellationToken);
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(cancellationToken);
            JToken root = JToken.Parse(json);

            JToken? rateToken = root.Type == JTokenType.Array
                ? root.First?["Rate"] ?? root.First?["rate"]
                : root["Rate"] ?? root["rate"];
            double rate = ParseRate(rateToken?.ToString());
            ValidateUzsRange(rate);
            return rate;
        }

        private static async Task<double> FetchFromErApiAsync(CancellationToken cancellationToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://open.er-api.com/v6/latest/USD");
            using HttpResponseMessage resp = await Http.SendAsync(req, cancellationToken);
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync(cancellationToken);
            JToken root = JToken.Parse(json);
            JToken? uzsToken = root["rates"]?["UZS"];
            double rate = ParseRate(uzsToken?.ToString());
            ValidateUzsRange(rate);
            return rate;
        }

        private static double ParseRate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new Exception("Kurs qiymati server javobida topilmadi.");
            }

            string normalized = raw.Trim().Replace(" ", string.Empty).Replace(",", ".");
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || parsed <= 0)
            {
                throw new Exception("Kurs qiymatini o'qib bo'lmadi.");
            }

            return parsed;
        }

        private static void ValidateUzsRange(double rate)
        {
            if (rate < 1000 || rate > 100000)
            {
                throw new Exception($"Kurs shubhali qiymat qaytardi: {rate.ToString("N2", CultureInfo.InvariantCulture)}");
            }
        }

        private static string ToFriendlyMessage(Exception ex)
        {
            if (ex is TaskCanceledException)
            {
                return "Server javobi kechikdi (timeout).";
            }

            if (ex is HttpRequestException hre && hre.InnerException is SocketException)
            {
                return "Internetga ulanish yo'q yoki tarmoq bloklangan.";
            }

            if (ex is HttpRequestException)
            {
                return "Kurs serveriga ulanishda xatolik.";
            }

            return ex.Message;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SantexnikaSRM/1.0");
            return client;
        }
    }
}
