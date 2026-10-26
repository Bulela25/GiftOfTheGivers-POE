using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gift_of_thegivers.Pages.Volunteer
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string FullName { get; set; }

        [BindProperty]
        public string Skills { get; set; }

        [BindProperty]
        public string Availability { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!string.IsNullOrWhiteSpace(FullName))
            {
                VolunteerStore.AllVolunteers.Add(new VolunteerEntry
                {
                    FullName = FullName,
                    Skills = Skills,
                    Availability = Availability,
                    SubmittedOn = DateTime.Now
                });
            }

            TempData["Message"] = "Thank you. Your interest has been recorded.";
            return RedirectToPage();
        }
    }
}