using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gift_of_thegivers.Pages.Donate
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string DonationType { get; set; } = "OneTime";

        [BindProperty]
        public decimal Amount { get; set; }

        [BindProperty]
        public string Currency { get; set; } = "ZAR";

        [BindProperty]
        public bool IsAnonymous { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();

                // Decide donor name
                string donorName = IsAnonymous
                    ? "Anonymous Guest"
                    : (HttpContext.Session.GetString("UserName") ?? "Guest");

                // Call the Azure Function (Function1)
                string url = "http://localhost:7234/api/Function1" +
                             $"?donorName={Uri.EscapeDataString(donorName)}" +
                             $"&amount={Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                             $"&currency={Uri.EscapeDataString(Currency)}" +
                             $"&type={Uri.EscapeDataString(DonationType)}";

                string certificate = await client.GetStringAsync(url);

                // Store the certificate and details for the next page
                TempData["Certificate"] = certificate;
                TempData["Amount"] = Amount.ToString();
                TempData["Currency"] = Currency;
                TempData["DonationType"] = DonationType;
                TempData["DonorName"] = donorName;

                return RedirectToPage("Certificate");
            }
            catch (Exception)
            {
                ErrorMessage = "Could not generate the tax certificate. Please make sure the Azure Function is running.";
                return Page();
            }
        }
    }
}