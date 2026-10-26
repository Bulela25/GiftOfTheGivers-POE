using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gift_of_thegivers.Pages.Account
{
    public class RegisterModel : PageModel
    {
        [BindProperty]
        public string FullName { get; set; }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        [BindProperty]
        public string Role { get; set; } = "Donor";

        public string Message { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                Message = "Please fill in all fields.";
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                Message = "Passwords do not match.";
                return Page();
            }

            try
            {
                using HttpClient client = new HttpClient();

                string url = "http://localhost:7234/api/Function1" +
                             $"?username={Uri.EscapeDataString(FullName)}" +
                             $"&email={Uri.EscapeDataString(Email)}" +
                             $"&password={Uri.EscapeDataString(Password)}" +
                             $"&role={Uri.EscapeDataString(Role)}";

                HttpResponseMessage response = await client.GetAsync(url);
                string result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Message = result; // "User registered successfully."
                    return RedirectToPage("/Account/Login");
                }
                else
                {
                    Message = result;
                }
            }
            catch (Exception ex)
            {
                Message = "Could not reach the registration service. Make sure the Azure Function is running. Error: " + ex.Message;
            }

            return Page();
        }
    }
}