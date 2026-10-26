using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gift_of_thegivers.Pages.Employee
{
    public class VolunteersModel : PageModel
    {
        public List<VolunteerEntry> Volunteers { get; set; }

        public void OnGet()
        {
            Volunteers = VolunteerStore.AllVolunteers
                .OrderByDescending(v => v.SubmittedOn)
                .ToList();
        }
    }
}
