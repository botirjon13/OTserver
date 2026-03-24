namespace SantexnikaSRM.Models
{
    public class CustomerOverview
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public int OpenDebtCount { get; set; }
        public double OutstandingUZS { get; set; }
    }
}
