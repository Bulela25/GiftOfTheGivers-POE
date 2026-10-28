using Microsoft.Data.SqlClient;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using DonatePage = gift_of_thegivers.Pages.Donate.IndexModel;

namespace GiftOfTheGivers.Tests.Integration
{
    // A.2 Integration test: the full donation flow  form > Azure Function > database.
    // The website is hosted in memory by WebApplicationFactory and the donation form is submitted for real.
    // Needs: the Azure Function running (givers_function on port 7234) and the database set up.
    [Trait("Category", "Integration")]
    public class DonationFlowTests : IClassFixture<WebApplicationFactory<DonatePage>>
    {
        private readonly WebApplicationFactory<DonatePage> _factory;

        public DonationFlowTests(WebApplicationFactory<DonatePage> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AnonymousDonation_FromForm_ThroughFunction_IsSavedToDatabase()
        {
            HttpClient client = _factory.CreateClient();

            // 1. Open the donation form and read its anti-forgery token
            HttpResponseMessage formPage = await client.GetAsync("/Donate/Index");
            Assert.Equal(HttpStatusCode.OK, formPage.StatusCode);

            string formHtml = await formPage.Content.ReadAsStringAsync();
            string token = Regex.Match(formHtml,
                "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(token), "Anti-forgery token not found on the Donate page.");

            // 2. Submit the form as an anonymous donor
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["DonationType"] = "OneTime",
                ["Amount"] = "375",
                ["Currency"] = "ZAR",
                ["IsAnonymous"] = "true",
                ["__RequestVerificationToken"] = token
            });

            HttpResponseMessage response = await client.PostAsync("/Donate/Index", form);
            string html = await response.Content.ReadAsStringAsync();

            Assert.True(response.IsSuccessStatusCode, $"Form post failed with {response.StatusCode}.");
            Assert.DoesNotContain("Could not generate the tax certificate", html);   // Function was reached

            // 3. The certificate page shows the certificate created by the Function
            Assert.EndsWith("/Donate/Certificate", response.RequestMessage!.RequestUri!.AbsolutePath);
            Assert.Contains("Anonymous Guest", html);
            Assert.Contains("375.00 ZAR", html);

            string reference = Regex.Match(html, @"GOTG-\d{8}-[0-9A-F]{8}").Value;
            Assert.False(string.IsNullOrEmpty(reference), "No reference number on the certificate page.");

            // 4. The donation is in the database
            using SqlConnection conn = await TestSettings.OpenDatabaseAsync();
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT DonorName, Amount, Currency FROM Donations WHERE Reference = @Ref", conn);
                cmd.Parameters.AddWithValue("@Ref", reference);

                using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync(), $"Donation {reference} not found in the database.");
                Assert.Equal("Anonymous Guest", reader.GetString(0));
                Assert.Equal(375m, reader.GetDecimal(1));
                Assert.Equal("ZAR", reader.GetString(2));
            }
            finally
            {
                using var delete = new SqlCommand("DELETE FROM Donations WHERE Reference = @Ref", conn);
                delete.Parameters.AddWithValue("@Ref", reference);
                await delete.ExecuteNonQueryAsync();
            }
        }
    }
}
