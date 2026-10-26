using System.Data.SqlClient;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace GiftOfTheGivers.Tests.UI
{
    // A.4 Selenium UI tests — drives a real Chrome window through the site.
    // Needs: Google Chrome installed, the website (port 5191) AND the Azure Function (port 7234) running.
    // Selenium Manager downloads the matching ChromeDriver automatically the first time.
    // Set the environment variable GOTG_HEADLESS=true to run without opening a visible window.
    [Trait("Category", "UI")]
    public class SeleniumUiTests : IDisposable
    {
        private readonly IWebDriver _driver;
        private readonly WebDriverWait _wait;
        private readonly ITestOutputHelper _output;
        private readonly List<string> _emailsToDelete = new();
        private readonly List<string> _referencesToDelete = new();

        public SeleniumUiTests(ITestOutputHelper output)
        {
            _output = output;

            var options = new ChromeOptions();
            options.AddArgument("--window-size=1400,900");
            options.AcceptInsecureCertificates = true;   // allow the local dev HTTPS certificate
            if (Environment.GetEnvironmentVariable("GOTG_HEADLESS") == "true")
            {
                options.AddArgument("--headless=new");
            }

            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        }

        private string Url(string path) => TestSettings.WebBaseUrl + path;

        private void Type(string elementId, string text)
        {
            IWebElement input = _driver.FindElement(By.Id(elementId));
            input.Clear();
            input.SendKeys(text);
        }

        private void SaveScreenshot(string name)
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ((ITakesScreenshot)_driver).GetScreenshot().SaveAsFile(file);
            _output.WriteLine($"Screenshot saved: {file}");
        }

        // ---------- Scenario 1: Employee login ----------
        [Fact]
        public void Employee_CanRegister_LogIn_AndReachStaffDashboard()
        {
            string email = $"selenium_employee_{Guid.NewGuid():N}@test.local";
            const string password = "Employee123!";
            const string name = "Selenium Employee";
            _emailsToDelete.Add(email);

            // Arrange: create an employee account through the Register page
            _driver.Navigate().GoToUrl(Url("/Account/Register"));
            Type("FullName", name);
            Type("Email", email);
            Type("Password", password);
            Type("ConfirmPassword", password);
            _driver.FindElement(By.Id("roleEmployee")).Click();
            _driver.FindElement(By.CssSelector("form button[type='submit']")).Click();

            _wait.Until(d => d.Url.Contains("/Account/Login"));

            // Act: log in as that employee
            Type("Email", email);
            Type("Password", password);
            _driver.FindElement(By.CssSelector("form button[type='submit']")).Click();

            // Assert: redirected to the staff dashboard, greeted by name, staff tools visible
            _wait.Until(d => d.Url.Contains("/Employee/Dashboard"));

            Assert.Equal("Staff Dashboard", _driver.FindElement(By.TagName("h1")).Text);
            Assert.Contains($"Hello, {name}", _driver.FindElement(By.TagName("nav")).Text);
            Assert.NotEmpty(_driver.FindElements(By.CssSelector("a[href='/Employee/ProjectUpdates']")));
            Assert.NotEmpty(_driver.FindElements(By.CssSelector("a[href='/Employee/Volunteers']")));

            SaveScreenshot("EmployeeLogin_Dashboard");
        }

        // ---------- Scenario 2: Anonymous donation ----------
        [Fact]
        public void Donor_CanDonateAnonymously_AndReceiveCertificate()
        {
            _driver.Navigate().GoToUrl(Url("/Donate/Index"));

            // Fill in the donation form
            _driver.FindElement(By.Id("oneTime")).Click();
            Type("Amount", "500");
            _driver.FindElement(By.Id("anonymous")).Click();
            Assert.True(_driver.FindElement(By.Id("anonymous")).Selected);

            SaveScreenshot("AnonymousDonation_Form");
            _driver.FindElement(By.CssSelector("form button[type='submit']")).Click();

            // Assert: the certificate page shows an anonymous certificate from the Azure Function
            _wait.Until(d => d.Url.Contains("/Donate/Certificate"));

            string certificate = _driver.FindElement(By.TagName("pre")).Text;
            Assert.Contains("TEMPORARY TAX CERTIFICATE", certificate);
            Assert.Contains("Anonymous Guest", certificate);
            Assert.Contains("500.00 ZAR", certificate);
            Assert.Contains("OneTime", certificate);

            string reference = Regex.Match(certificate, @"GOTG-\d{8}-[0-9A-F]{8}").Value;
            Assert.False(string.IsNullOrEmpty(reference), "Certificate has no reference number.");
            _referencesToDelete.Add(reference);

            SaveScreenshot("AnonymousDonation_Certificate");
        }

        // ---------- Scenario 3: Wrong password is refused ----------
        [Fact]
        public void Login_WithWrongPassword_StaysOnLoginPage()
        {
            _driver.Navigate().GoToUrl(Url("/Account/Login"));

            Type("Email", "nobody@test.local");
            Type("Password", "WrongPassword!");
            _driver.FindElement(By.CssSelector("form button[type='submit']")).Click();

            _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");

            Assert.Contains("/Account/Login", _driver.Url);
            Assert.DoesNotContain("Logout", _driver.FindElement(By.TagName("nav")).Text);

            SaveScreenshot("Login_WrongPassword");
        }

        public void Dispose()
        {
            _driver.Quit();
            _driver.Dispose();
            CleanUpDatabase();
        }

        private void CleanUpDatabase()
        {
            if (_emailsToDelete.Count == 0 && _referencesToDelete.Count == 0) return;
            try
            {
                using var conn = new SqlConnection(TestSettings.SqlConnectionString);
                conn.Open();
                foreach (string email in _emailsToDelete)
                {
                    using var cmd = new SqlCommand("DELETE FROM Users WHERE Email = @Email", conn);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.ExecuteNonQuery();
                }
                foreach (string reference in _referencesToDelete)
                {
                    using var cmd = new SqlCommand("DELETE FROM Donations WHERE Reference = @Ref", conn);
                    cmd.Parameters.AddWithValue("@Ref", reference);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Cleanup only.
            }
        }
    }
}
