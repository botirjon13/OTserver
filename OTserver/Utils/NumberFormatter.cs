using System.Globalization;

namespace SantexnikaSRM.Utils
{
    public static class NumberFormatter
    {
        private static readonly CultureInfo uzCulture = new CultureInfo("uz-UZ");

        public static string FormatUZS(double amount)
        {
            return string.Format(uzCulture, "{0:N0} so'm", amount);
        }

        public static string FormatUSD(double amount)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:N2} $", amount);
        }

        public static string FormatNumber(double number)
        {
            return string.Format(uzCulture, "{0:N0}", number);
        }
    }
}