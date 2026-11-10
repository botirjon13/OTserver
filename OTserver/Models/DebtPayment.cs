using System;

namespace SantexnikaSRM.Models
{
    public class DebtPayment
    {
        public int Id { get; set; }
        public int DebtId { get; set; }
        public double AmountUZS { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
    }
}
