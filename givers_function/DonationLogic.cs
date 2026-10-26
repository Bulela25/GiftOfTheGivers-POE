using System.Globalization;

namespace givers_function
{
    /// <summary>
    /// Pure donation rules, kept separate from the Azure Function so they can be unit tested
    /// without a web server or a database.
    /// </summary>
    public static class DonationLogic
    {
        public const decimal MinimumAmount = 1m;
        public const decimal MaximumAmount = 1_000_000m;

        public static readonly string[] SupportedCurrencies = { "ZAR", "USD", "EUR" };
        public static readonly string[] SupportedTypes = { "OneTime", "Recurring" };

        /// <summary>
        /// Checks a donation request. Returns true when valid; otherwise false with a reason.
        /// The amount is read with the invariant culture so "150.50" works on any PC.
        /// </summary>
        public static bool TryValidate(string? amountText, string? currency, string? donationType,
                                       out decimal amount, out string error)
        {
            amount = 0m;

            if (string.IsNullOrWhiteSpace(amountText) ||
                !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                error = "Amount must be a number.";
                return false;
            }

            if (amount < MinimumAmount)
            {
                error = $"Amount must be at least {MinimumAmount}.";
                return false;
            }

            if (amount > MaximumAmount)
            {
                error = $"Amount cannot be more than {MaximumAmount:N0}.";
                return false;
            }

            if (!SupportedCurrencies.Contains(currency ?? "", StringComparer.OrdinalIgnoreCase))
            {
                error = "Currency must be ZAR, USD or EUR.";
                return false;
            }

            if (!SupportedTypes.Contains(donationType ?? "", StringComparer.OrdinalIgnoreCase))
            {
                error = "Donation type must be OneTime or Recurring.";
                return false;
            }

            error = "";
            return true;
        }

        /// <summary>
        /// Total pledged over a year: a one-time gift counts once, a monthly recurring gift counts 12 times.
        /// </summary>
        public static decimal CalculateAnnualTotal(decimal amount, string donationType)
        {
            return string.Equals(donationType, "Recurring", StringComparison.OrdinalIgnoreCase)
                ? Math.Round(amount * 12, 2)
                : Math.Round(amount, 2);
        }

        /// <summary>
        /// Sum of several donations, e.g. for a donor's history or a campaign total.
        /// </summary>
        public static decimal CalculateTotal(IEnumerable<decimal> amounts)
        {
            return Math.Round(amounts.Sum(), 2);
        }

        /// <summary>
        /// Unique reference number, e.g. GOTG-20261026-1A2B3C4D.
        /// </summary>
        public static string CreateReference(DateTime now)
        {
            return $"GOTG-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }

        /// <summary>
        /// Builds the placeholder tax certificate text.
        /// </summary>
        public static string FormatCertificate(string? donorName, decimal amount, string currency,
                                               string donationType, DateTime date, string reference)
        {
            string name = string.IsNullOrWhiteSpace(donorName) ? "Anonymous Guest" : donorName.Trim();
            string amountText = amount.ToString("N2", CultureInfo.InvariantCulture);
            string annualText = CalculateAnnualTotal(amount, donationType).ToString("N2", CultureInfo.InvariantCulture);

            return $@"
===============================================
        GIFT OF THE GIVERS
     TEMPORARY TAX CERTIFICATE
===============================================

This is a placeholder certificate for demonstration purposes only.

Donor Name     : {name}
Amount         : {amountText} {currency.ToUpperInvariant()}
Donation Type  : {donationType}
Annual Total   : {annualText} {currency.ToUpperInvariant()}
Date           : {date.ToString("dd MMMM yyyy HH:mm", CultureInfo.InvariantCulture)}
Reference No.  : {reference}

-----------------------------------------------
Note: This is NOT an official SARS tax certificate.
A real certificate would be issued after the
donation has been processed and confirmed.
===============================================
";
        }
    }
}
