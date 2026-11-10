using System;

namespace SantexnikaSRM.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public required string Type { get; set; }
        public required string Description { get; set; }
        public double AmountUZS { get; set; }
    }
}