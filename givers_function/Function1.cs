using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using BCrypt.Net;

namespace givers_function
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        // Can be overridden with the GOTG_SQL environment variable (used by the tests and on Azure).
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("GOTG_SQL")
            ?? @"Server=(localdb)\ngo;Database=gift_of_thegivers;Trusted_Connection=True;";

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function("Function1")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            // Check if this is a Donation (Tax Certificate) request
            string amount = req.Query["amount"].ToString();

            if (!string.IsNullOrWhiteSpace(amount))
            {
                return await HandleDonationAsync(req, amount);
            }

            // ========== REGISTRATION ==========
            string fullName = req.Query["username"].ToString();
            string email = req.Query["email"].ToString();
            string password = req.Query["password"].ToString();
            string role = string.IsNullOrWhiteSpace(req.Query["role"]) ? "Donor" : req.Query["role"].ToString();

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return new BadRequestObjectResult("Full name, email and password are required.");
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            try
            {
                using SqlConnection conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                // Check if email already exists
                string checkSql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
                using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Email", email);
                    int exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);

                    if (exists > 0)
                    {
                        return new ConflictObjectResult("This email is already registered.");
                    }
                }

                // Insert new user
                string insertSql = @"
                    INSERT INTO Users (FullName, Email, PasswordHash, Role)
                    VALUES (@FullName, @Email, @PasswordHash, @Role)";

                using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@FullName", fullName);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                    cmd.Parameters.AddWithValue("@Role", role);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows > 0)
                    {
                        _logger.LogInformation("User registered: {Email}", email);
                        return new OkObjectResult("User registered successfully.");
                    }

                    return new StatusCodeResult(500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering user");
                return new ObjectResult("Database error: " + ex.Message) { StatusCode = 500 };
            }
        }

        // ========== DONATION + TAX CERTIFICATE ==========
        private async Task<IActionResult> HandleDonationAsync(HttpRequest req, string amountText)
        {
            string donorName = req.Query["donorName"].ToString();
            string currency = req.Query["currency"].ToString();
            string donationType = req.Query["type"].ToString();

            donorName = string.IsNullOrWhiteSpace(donorName) ? "Anonymous Guest" : donorName;
            currency = string.IsNullOrWhiteSpace(currency) ? "ZAR" : currency.ToUpperInvariant();
            donationType = string.IsNullOrWhiteSpace(donationType) ? "OneTime" : donationType;

            if (!DonationLogic.TryValidate(amountText, currency, donationType, out decimal amount, out string error))
            {
                return new BadRequestObjectResult(error);
            }

            DateTime now = DateTime.Now;
            string reference = DonationLogic.CreateReference(now);

            try
            {
                using SqlConnection conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                string insertSql = @"
                    INSERT INTO Donations (Reference, DonorName, Amount, Currency, DonationType)
                    VALUES (@Reference, @DonorName, @Amount, @Currency, @DonationType)";

                using SqlCommand cmd = new SqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("@Reference", reference);
                cmd.Parameters.AddWithValue("@DonorName", donorName);
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Currency", currency);
                cmd.Parameters.AddWithValue("@DonationType", donationType);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving donation");
                return new ObjectResult("Database error: " + ex.Message) { StatusCode = 500 };
            }

            string certificate = DonationLogic.FormatCertificate(donorName, amount, currency, donationType, now, reference);

            _logger.LogInformation("Donation {Reference} saved and certificate generated for {Donor}", reference, donorName);
            return new OkObjectResult(certificate);
        }
    }
}
