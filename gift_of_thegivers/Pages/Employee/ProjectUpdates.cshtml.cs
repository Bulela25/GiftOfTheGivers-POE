using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gift_of_thegivers.Pages.Employee
{
    public class ProjectUpdate
    {
        public string Title { get; set; }
        public string Location { get; set; }
        public string Details { get; set; }
        public DateTime PostedOn { get; set; }
    }

    public class ProjectUpdatesModel : PageModel
    {
        // Simple in-memory store for the prototype
        public static List<ProjectUpdate> AllUpdates = new List<ProjectUpdate>();

        [BindProperty]
        public string Title { get; set; }

        [BindProperty]
        public string Location { get; set; }

        [BindProperty]
        public string Details { get; set; }

        public List<ProjectUpdate> Updates { get; set; }

        public void OnGet()
        {
            Updates = AllUpdates.OrderByDescending(u => u.PostedOn).ToList();
        }

        public IActionResult OnPost()
        {
            if (!string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Details))
            {
                AllUpdates.Add(new ProjectUpdate
                {
                    Title = Title,
                    Location = Location ?? "Not specified",
                    Details = Details,
                    PostedOn = DateTime.Now
                });
            }

            return RedirectToPage();
        }
    }
}