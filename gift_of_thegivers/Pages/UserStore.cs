namespace gift_of_thegivers.Pages
{
    public class AppUser
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }   // "Donor" or "Employee"
    }

    public static class UserStore
    {
        public static List<AppUser> Users = new List<AppUser>();
    }
}