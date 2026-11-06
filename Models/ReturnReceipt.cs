using System;
using System.Collections.Generic;

namespace SantexnikaSRM.Models
{
    public class ReturnReceipt
    {
        public int ReturnId { get; set; }
        public int SaleId { get; set; }
        public string ReceiptNumber => $"RET-{ReturnId:D6}";
        public string OriginalReceiptNumber { get; set; } = "";
        public DateTime IssuedAt { get; set; }
        public string Reason { get; set; } = "";
        public string BusinessName { get; set; } = "";
        public string TIN { get; set; } = "";
        public string StoreAddress { get; set; } = "";
        public string StorePhone { get; set; } = "";
        public byte[] LogoPng { get; set; } = Array.Empty<byte>();
        public double SubtotalUZS { get; set; }
        public double DiscountUZS { get; set; }
        public double TotalUZS { get; set; }
        public double DebtReducedUZS { get; set; }
        public double CashRefundUZS { get; set; }
        public List<ReturnReceiptItem> Items { get; set; } = new List<ReturnReceiptItem>();
    }

    public class ReturnReceiptItem
    {
        public string ProductName { get; set; } = "";
        public double Quantity { get; set; }
        public double UnitPriceUZS { get; set; }
        public double LineTotalUZS { get; set; }
    }
}
