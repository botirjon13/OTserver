namespace SantexnikaSRM.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
    }
}
