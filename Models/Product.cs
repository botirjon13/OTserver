namespace SantexnikaSRM.Models
{
    public class Product
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public double PurchasePriceUSD { get; set; }
        public double QuantityUSD { get; set; }
    }
}