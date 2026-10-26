using System.Data.SqlClient;
using System.Text.RegularExpressions;
using givers_function;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GiftOfTheGivers.Tests.Integration
{
    // A.2 Integration tests: the real Azure Function code talking to the real SQL Server database.
    // Needs: LocalDB (localdb)\ngo with SQLQuery1.sql and SQLQuery2_Donations.sql run.
    // The Function host does NOT need to be running — the function class is called directly.
    [Trait("Category", "Integration")]
    public class FunctionDatabaseTests
    {
        private static HttpRequest BuildRequest(string queryString)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.QueryString = new QueryString(queryString);
            return context.Request;
        }

        [Fact]
        public async Task Donation_IsSavedToDatabase_AndCertificateReturned()
        {
            var function = new Function1(NullLogger<Function1>.Instance);
            var request = BuildRequest("?amount=250&currency=ZAR&type=Recurring&donorName=Integration%20Tester");

            // Act: call the function exactly as the website does
            IActionResult result = await function.Run(request);

            // Assert: the function returned a certificate
            var ok = Assert.IsType<OkObjectResult>(result);
            string certificate = Assert.IsType<string>(ok.Value);
            Assert.Contains("Donor Name     : Integration Tester", certificate);
            Assert.Contains("Annual Total   : 3,000.00 ZAR", certificate);

            string reference = Regex.Match(certificate, @"GOTG-\d{8}-[0-9A-F]{8}").Value;
            Assert.False(string.IsNullOrEmpty(reference), "Certificate should contain a reference number.");

            // Assert: the donation row exists in the database
            using SqlConnection conn = await TestSettings.OpenDatabaseAsync();
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT DonorName, Amount, Currency, DonationType FROM Donations WHERE Reference = @Ref", conn);
                cmd.Parameters.AddWithValue("@Ref", reference);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync(), $"Donation {reference} was not found in the database.");
                    Assert.Equal("Integration Tester", reader.GetString(0));
                    Assert.Equal(250m, reader.GetDecimal(1));
                    Assert.Equal("ZAR", reader.GetString(2));
                    Assert.Equal("Recurring", reader.GetString(3));
                }
            }
            finally
            {
                // Clean up the test row
                using var delete = new SqlCommand("DELETE FROM Donations WHERE Reference = @Ref", conn);
                delete.Parameters.AddWithValue("@Ref", reference);
                await delete.ExecuteNonQueryAsync();
            }
        }

        [Fact]
        public async Task InvalidDonation_IsRejected_AndNothingSaved()
        {
            var function = new Function1(NullLogger<Function1>.Instance);
            var request = BuildRequest("?amount=-10&currency=ZAR&type=OneTime&donorName=Should%20Not%20Save");

            IActionResult result = await function.Run(request);

            Assert.IsType<BadRequestObjectResult>(result);

            using SqlConnection conn = await TestSettings.OpenDatabaseAsync();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Donations WHERE DonorName = 'Should Not Save'", conn);
            Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync() ?? 0));
        }

        [Fact]
        public async Task Registration_SavesUserWithHashedPassword()
        {
            var function = new Function1(NullLogger<Function1>.Instance);
            string email = $"integration_{Guid.NewGuid():N}@test.local";
            var request = BuildRequest(
                $"?username=Integration%20User&email={Uri.EscapeDataString(email)}&password=Secret123!&role=Donor");

            IActionResult result = await function.Run(request);

            Assert.IsType<OkObjectResult>(result);

            using SqlConnection conn = await TestSettings.OpenDatabaseAsync();
            try
            {
                using var cmd = new SqlCommand("SELECT PasswordHash, Role FROM Users WHERE Email = @Email", conn);
                cmd.Parameters.AddWithValue("@Email", email);

                using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), "User was not saved.");

                string hash = reader.GetString(0);
                Assert.NotEqual("Secret123!", hash);                              // never stored as plain text
                Assert.True(BCrypt.Net.BCrypt.Verify("Secret123!", hash));        // but still verifies
                Assert.Equal("Donor", reader.GetString(1));
            }
            finally
            {
                using var delete = new SqlCommand("DELETE FROM Users WHERE Email = @Email", conn);
                delete.Parameters.AddWithValue("@Email", email);
                await delete.ExecuteNonQueryAsync();
            }
        }
    }
}
