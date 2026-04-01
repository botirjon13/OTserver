namespace SantexnikaSRM.Models
{
    public class Product
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string PurchaseCurrency { get; set; } = "USD";
        public double PurchasePrice { get; set; }
        public double PurchasePriceUZS { get; set; }
        public double PurchasePriceUSD { get; set; }
        public double QuantityUSD { get; set; }
        public string ImagePath { get; set; } = string.Empty;
    }
}
