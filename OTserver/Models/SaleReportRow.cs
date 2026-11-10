namespace SantexnikaSRM.Models
{
    public class SaleReportRow
    {
        public string ProductName { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public double SoldAmountUSD { get; set; }
        public double SoldAmountUZS { get; set; }
        public double ProfitUSD { get; set; }
        public double ProfitUZS { get; set; }
    }
}
