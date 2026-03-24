namespace SantexnikaSRM.Models
{
    public class SaleItem
    {
        public int Id { get; set; }

        public int SaleId { get; set; }

        public int ProductId { get; set; }

        // Nechta sotildi
        public double Quantity { get; set; }

        // Sotish narxi (UZS)
        public double SellPriceUZS { get; set; }

        // Shu item bo'yicha berilgan chegirma summasi (UZS)
        public double DiscountUZS { get; set; }

        // Shu item bo'yicha jami summa
        public double TotalUZS => Quantity * SellPriceUZS;

        // Shu item bo'yicha foyda (UZS)
        public double ProfitUZS { get; set; }
    }
}
