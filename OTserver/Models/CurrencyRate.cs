using System;

namespace SantexnikaSRM.Models
{
    public class CurrencyRate
    {
        public int Id { get; set; }

        // Dollar kursi
        public double Rate { get; set; }

        // Qaysi sanaga tegishli
        public DateTime Date { get; set; }
    }
}