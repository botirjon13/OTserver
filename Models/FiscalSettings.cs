namespace SantexnikaSRM.Models
{
    public class FiscalSettings
    {
        public int Id { get; set; }
        public string BusinessName { get; set; } = "";
        public string TIN { get; set; } = "";
        public string StoreAddress { get; set; } = "";
        public string KkmNumber { get; set; } = "";
        public string StorePhone { get; set; } = "";
        public string ThankYouMessage { get; set; } = "";
        public byte[] LogoPng { get; set; } = Array.Empty<byte>();
        public bool IsVatPayer { get; set; }
        public double VatRatePercent { get; set; }
    }
}
