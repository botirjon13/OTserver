using System;

namespace SantexnikaSRM.Models
{
    public class Debt
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int CustomerId { get; set; }
        public double TotalAmountUZS { get; set; }
        public double PaidAmountUZS { get; set; }
        public double RemainingAmountUZS { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Open";
        public DateTime CreatedAt { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
    }
}
