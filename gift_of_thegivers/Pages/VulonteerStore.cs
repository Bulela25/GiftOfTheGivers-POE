namespace gift_of_thegivers.Pages
{

    public class VolunteerEntry
    {
        public string FullName { get; set; }
        public string Skills { get; set; }
        public string Availability { get; set; }
        public DateTime SubmittedOn { get; set; }
    }

    public static class VolunteerStore
    {
        public static List<VolunteerEntry> AllVolunteers = new List<VolunteerEntry>();
    }
}