namespace SantexnikaSRM.Models
{
    public class DebtSummary
    {
        public double OutstandingUZS { get; set; }
        public double OverdueUZS { get; set; }
        public int OpenDebts { get; set; }
        public int OverdueDebts { get; set; }
    }
}
