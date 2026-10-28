using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace GiftOfTheGivers.Tests.Load
{
    public class LoadTestConfig
    {
        public int VirtualUsers { get; set; } = 5;
        public int DurationSeconds { get; set; } = 30;
        public int ThinkTimeMilliseconds { get; set; } = 500;
        public double MaxAverageResponseMs { get; set; } = 1000;
        public double MaxP95ResponseMs { get; set; } = 2000;
        public double MaxErrorRatePercent { get; set; } = 1;
        public List<LoadScenario> Scenarios { get; set; } = new();
    }

    public class LoadScenario
    {
        public string Name { get; set; } = "";
        public string Target { get; set; } = "Web";   // "Web" or "Function"
        public string Path { get; set; } = "/";
    }

    // A.3 Load and stress test: 5 concurrent users for 30 seconds (settings in loadtest.settings.json).
    // Needs: the website (port 5191) AND the Azure Function (port 7234) running.
    [Trait("Category", "Load")]
    public class LoadTests
    {
        private readonly ITestOutputHelper _output;

        public LoadTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private record Sample(string Scenario, double Milliseconds, bool Success, int StatusCode);

        [Fact]
        public async Task FiveConcurrentUsers_SiteStaysFastAndErrorFree()
        {
            string settingsPath = Path.Combine(AppContext.BaseDirectory, "loadtest.settings.json");
            var config = JsonSerializer.Deserialize<LoadTestConfig>(
                File.ReadAllText(settingsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var samples = new ConcurrentBag<Sample>();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var testClock = Stopwatch.StartNew();
            var stopAt = DateTime.UtcNow.AddSeconds(config.DurationSeconds);

            // Each virtual user loops through every scenario until the time is up
            async Task VirtualUser(int userNumber)
            {
                while (DateTime.UtcNow < stopAt)
                {
                    foreach (var scenario in config.Scenarios)
                    {
                        if (DateTime.UtcNow >= stopAt) break;

                        string url = scenario.Target.Equals("Function", StringComparison.OrdinalIgnoreCase)
                            ? TestSettings.FunctionUrl + scenario.Path
                            : TestSettings.WebBaseUrl + scenario.Path;

                        var timer = Stopwatch.StartNew();
                        try
                        {
                            using HttpResponseMessage response = await http.GetAsync(url);
                            await response.Content.ReadAsStringAsync();
                            timer.Stop();
                            samples.Add(new Sample(scenario.Name, timer.Elapsed.TotalMilliseconds,
                                                   response.IsSuccessStatusCode, (int)response.StatusCode));
                        }
                        catch (Exception)
                        {
                            timer.Stop();
                            samples.Add(new Sample(scenario.Name, timer.Elapsed.TotalMilliseconds, false, 0));
                        }

                        await Task.Delay(config.ThinkTimeMilliseconds);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(1, config.VirtualUsers).Select(VirtualUser));
            testClock.Stop();

            // ---------- Report ----------
            var all = samples.ToList();
            Assert.NotEmpty(all);

            var report = new StringBuilder();
            report.AppendLine("GIFT OF THE GIVERS - LOAD TEST RESULTS");
            report.AppendLine($"Run at          : {DateTime.Now:dd MMM yyyy HH:mm:ss}");
            report.AppendLine($"Virtual users   : {config.VirtualUsers}");
            report.AppendLine($"Duration        : {testClock.Elapsed.TotalSeconds:F1} s");
            report.AppendLine($"Think time      : {config.ThinkTimeMilliseconds} ms");
            report.AppendLine($"Website         : {TestSettings.WebBaseUrl}");
            report.AppendLine($"Function        : {TestSettings.FunctionUrl}");
            report.AppendLine();
            report.AppendLine($"{"Scenario",-18}{"Requests",10}{"Errors",8}{"Avg ms",10}{"P95 ms",10}{"Max ms",10}");
            report.AppendLine(new string('-', 66));

            foreach (var group in all.GroupBy(s => s.Scenario))
            {
                report.AppendLine(Row(group.Key, group.ToList()));
            }

            report.AppendLine(new string('-', 66));
            report.AppendLine(Row("TOTAL", all));
            report.AppendLine();

            double avg = all.Average(s => s.Milliseconds);
            double p95 = Percentile(all.Select(s => s.Milliseconds), 95);
            double errorRate = 100.0 * all.Count(s => !s.Success) / all.Count;
            double throughput = all.Count / testClock.Elapsed.TotalSeconds;

            report.AppendLine($"Throughput      : {throughput:F1} requests/second");
            report.AppendLine($"Error rate      : {errorRate:F2} %   (limit {config.MaxErrorRatePercent} %)");
            report.AppendLine($"Average         : {avg:F0} ms       (limit {config.MaxAverageResponseMs} ms)");
            report.AppendLine($"95th percentile : {p95:F0} ms       (limit {config.MaxP95ResponseMs} ms)");

            var failedCodes = all.Where(s => !s.Success).GroupBy(s => s.StatusCode).ToList();
            if (failedCodes.Any())
            {
                report.AppendLine("Failures by status code: " +
                    string.Join(", ", failedCodes.Select(g => $"{(g.Key == 0 ? "no response" : g.Key)} x{g.Count()}")));
            }

            string resultsFile = Path.Combine(AppContext.BaseDirectory, "LoadTestResults.txt");
            File.WriteAllText(resultsFile, report.ToString());
            report.AppendLine();
            report.AppendLine($"Saved to: {resultsFile}");
            _output.WriteLine(report.ToString());

            await RemoveLoadTestDonationsAsync();

            // ---------- Pass / fail ----------
            Assert.True(errorRate <= config.MaxErrorRatePercent, $"Error rate {errorRate:F2}% is above the limit.");
            Assert.True(avg <= config.MaxAverageResponseMs, $"Average {avg:F0} ms is above the limit.");
            Assert.True(p95 <= config.MaxP95ResponseMs, $"95th percentile {p95:F0} ms is above the limit.");
        }

        private static string Row(string name, List<Sample> s) =>
            $"{name,-18}{s.Count,10}{s.Count(x => !x.Success),8}" +
            $"{s.Average(x => x.Milliseconds),10:F0}{Percentile(s.Select(x => x.Milliseconds), 95),10:F0}" +
            $"{s.Max(x => x.Milliseconds),10:F0}";

        private static double Percentile(IEnumerable<double> values, double percentile)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return 0;
            int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
        }

        // The certificate scenario saves real donations; remove them so the database stays clean.
        private static async Task RemoveLoadTestDonationsAsync()
        {
            try
            {
                using SqlConnection conn = await TestSettings.OpenDatabaseAsync();
                using var cmd = new SqlCommand("DELETE FROM Donations WHERE DonorName = 'Load Test User'", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Cleanup only; never fail the load test because of it.
            }
        }
    }
}
