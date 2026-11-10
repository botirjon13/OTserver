using System;
using System.Collections.Generic;

namespace SantexnikaSRM.Models
{
    public class SaleReceipt
    {
        public int SaleId { get; set; }
        public string ReceiptNumber { get; set; } = "";
        public DateTime IssuedAt { get; set; }
        public string PaymentType { get; set; } = "";

        public string BusinessName { get; set; } = "";
        public string TIN { get; set; } = "";
        public string StoreAddress { get; set; } = "";
        public string StorePhone { get; set; } = "";
        public string ThankYouMessage { get; set; } = "";
        public string SellerName { get; set; } = "";
        public byte[] LogoPng { get; set; } = Array.Empty<byte>();
        public double InitialPaymentUZS { get; set; }
        public double RemainingDebtUZS { get; set; }

        public bool IsVatPayer { get; set; }
        public double VatRatePercent { get; set; }
        public double VatAmount { get; set; }
        public double SubtotalUZS { get; set; }
        public double DiscountUZS { get; set; }
        public string DiscountType { get; set; } = "None";
        public double DiscountValue { get; set; }
        public double TotalUZS { get; set; }

        public List<SaleReceiptItem> Items { get; set; } = new List<SaleReceiptItem>();
    }

    public class SaleReceiptItem
    {
        public int SaleItemId { get; set; }
        public string ProductName { get; set; } = "";
        public double Quantity { get; set; }
        public double UnitPriceUZS { get; set; }
        public double LineTotalUZS => Quantity * UnitPriceUZS;
    }
}
