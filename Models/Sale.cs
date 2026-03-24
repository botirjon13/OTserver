using System;
using System.Collections.Generic;

namespace SantexnikaSRM.Models
{
    public class Sale
    {
        public int Id { get; set; }

        // Sotuv sanasi
        public DateTime Date { get; set; }

        // Jami sotuv summasi (UZS)
        public double TotalUZS { get; set; }

        // Chegirmadan oldingi jami summa
        public double SubtotalUZS { get; set; }

        // Tezkor chegirma turi: None / Percent / Amount
        public string DiscountType { get; set; } = "None";

        // Foydalanuvchi kiritgan qiymat (foiz yoki summa)
        public double DiscountValue { get; set; }

        // Amalda qo'llangan chegirma summasi
        public double DiscountUZS { get; set; }

        // Jami foyda (UZS)
        public double ProfitUZS { get; set; }

        // Navigatsiya uchun (bir sotuvda bir nechta tovar bo'ladi)
        public List<SaleItem> Items { get; set; } = new List<SaleItem>();
    }
}
