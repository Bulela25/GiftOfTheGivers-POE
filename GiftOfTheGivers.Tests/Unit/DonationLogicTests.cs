using givers_function;
using Xunit;

namespace GiftOfTheGivers.Tests.Unit
{
    // A.1 Unit tests — no database, website or Azure Function needs to be running.
    [Trait("Category", "Unit")]
    public class DonationLogicTests
    {
        // ---------- Donation validation ----------

        [Theory]
        [InlineData("150", "ZAR", "OneTime")]
        [InlineData("99.99", "USD", "Recurring")]
        [InlineData("1", "eur", "onetime")]
        public void TryValidate_ValidDonation_ReturnsTrue(string amount, string currency, string type)
        {
            bool valid = DonationLogic.TryValidate(amount, currency, type, out decimal parsed, out string error);

            Assert.True(valid);
            Assert.Equal(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), parsed);
            Assert.Equal("", error);
        }

        [Theory]
        [InlineData("0", "Amount must be at least")]
        [InlineData("-50", "Amount must be at least")]
        [InlineData("abc", "Amount must be a number")]
        [InlineData("", "Amount must be a number")]
        [InlineData("2000000", "Amount cannot be more than")]
        public void TryValidate_BadAmount_ReturnsFalseWithReason(string amount, string expectedError)
        {
            bool valid = DonationLogic.TryValidate(amount, "ZAR", "OneTime", out _, out string error);

            Assert.False(valid);
            Assert.StartsWith(expectedError, error);
        }

        [Fact]
        public void TryValidate_UnsupportedCurrency_ReturnsFalse()
        {
            bool valid = DonationLogic.TryValidate("100", "GBP", "OneTime", out _, out string error);

            Assert.False(valid);
            Assert.Contains("Currency", error);
        }

        [Fact]
        public void TryValidate_UnknownDonationType_ReturnsFalse()
        {
            bool valid = DonationLogic.TryValidate("100", "ZAR", "Weekly", out _, out string error);

            Assert.False(valid);
            Assert.Contains("Donation type", error);
        }

        // ---------- Total calculation ----------

        [Theory]
        [InlineData("100", "OneTime", "100")]
        [InlineData("100", "Recurring", "1200")]
        [InlineData("49.99", "Recurring", "599.88")]
        public void CalculateAnnualTotal_ReturnsCorrectTotal(string amount, string type, string expected)
        {
            // Attributes can't hold decimals, so the values are passed as text and parsed here
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            decimal result = DonationLogic.CalculateAnnualTotal(decimal.Parse(amount, culture), type);

            Assert.Equal(decimal.Parse(expected, culture), result);
        }

        [Fact]
        public void CalculateTotal_AddsAllDonations()
        {
            var donations = new List<decimal> { 100m, 250.50m, 49.50m };

            Assert.Equal(400m, DonationLogic.CalculateTotal(donations));
        }

        [Fact]
        public void CalculateTotal_NoDonations_ReturnsZero()
        {
            Assert.Equal(0m, DonationLogic.CalculateTotal(new List<decimal>()));
        }

        // ---------- Certificate formatting ----------

        [Fact]
        public void FormatCertificate_IncludesAllDonationDetails()
        {
            var date = new DateTime(2026, 10, 26, 14, 30, 0);

            string certificate = DonationLogic.FormatCertificate(
                "Nyiko", 1500m, "zar", "OneTime", date, "GOTG-20261026-ABCD1234");

            Assert.Contains("GIFT OF THE GIVERS", certificate);
            Assert.Contains("Donor Name     : Nyiko", certificate);
            Assert.Contains("Amount         : 1,500.00 ZAR", certificate);
            Assert.Contains("Donation Type  : OneTime", certificate);
            Assert.Contains("Date           : 26 October 2026 14:30", certificate);
            Assert.Contains("Reference No.  : GOTG-20261026-ABCD1234", certificate);
            Assert.Contains("NOT an official SARS tax certificate", certificate);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FormatCertificate_NoDonorName_UsesAnonymousGuest(string? donorName)
        {
            string certificate = DonationLogic.FormatCertificate(
                donorName, 50m, "ZAR", "OneTime", DateTime.Now, "GOTG-TEST");

            Assert.Contains("Donor Name     : Anonymous Guest", certificate);
        }

        [Fact]
        public void FormatCertificate_Recurring_ShowsAnnualTotal()
        {
            string certificate = DonationLogic.FormatCertificate(
                "Donor", 200m, "USD", "Recurring", DateTime.Now, "GOTG-TEST");

            Assert.Contains("Annual Total   : 2,400.00 USD", certificate);
        }

        [Fact]
        public void CreateReference_IsUniqueAndWellFormed()
        {
            var now = new DateTime(2026, 10, 26);

            string first = DonationLogic.CreateReference(now);
            string second = DonationLogic.CreateReference(now);

            Assert.Matches(@"^GOTG-20261026-[0-9A-F]{8}$", first);
            Assert.NotEqual(first, second);
        }
    }
}
