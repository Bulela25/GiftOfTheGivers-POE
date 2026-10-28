using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using BCrypt;

namespace gift_of_thegivers.Pages.Account
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public bool RememberMe { get; set; }

        public string Message { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                Message = "Email and password are required.";
                return Page();
            }

            string connectionString = AppConfig.SqlConnectionString;

            try
            {
                using SqlConnection conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                string sql = "SELECT FullName, PasswordHash, Role FROM Users WHERE Email = @Email";
                using SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    Message = "Invalid email or password.";
                    return Page();
                }

                string fullName = reader.GetString(0);
                string storedHash = reader.GetString(1);
                string role = reader.GetString(2);

                // Verify password
                bool valid = BCrypt.Net.BCrypt.Verify(Password, storedHash);

                if (!valid)
                {
                    Message = "Invalid email or password.";
                    return Page();
                }

                // Store user info in Session
                HttpContext.Session.SetString("UserEmail", Email);
                HttpContext.Session.SetString("UserName", fullName);
                HttpContext.Session.SetString("UserRole", role);

                // Redirect based on role
                if (role == "Employee")
                {
                    return RedirectToPage("/Employee/Dashboard");
                }

                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                Message = "Login error: " + ex.Message;
                return Page();
            }
        }
    }
}