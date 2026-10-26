using System.Data.SqlClient;

namespace GiftOfTheGivers.Tests
{
    /// <summary>
    /// Addresses used by the integration, load and UI tests.
    /// Defaults match launchSettings.json; override with environment variables if yours differ.
    /// </summary>
    public static class TestSettings
    {
        public static string SqlConnectionString =>
            Environment.GetEnvironmentVariable("GOTG_SQL")
            ?? @"Server=(localdb)\ngo;Database=gift_of_thegivers;Trusted_Connection=True;";

        // Website ("http" profile in gift_of_thegivers/Properties/launchSettings.json)
        public static string WebBaseUrl =>
            (Environment.GetEnvironmentVariable("GOTG_WEB_URL") ?? "http://localhost:5191").TrimEnd('/');

        // Azure Function (givers_function/Properties/launchSettings.json uses --port 7234)
        public static string FunctionUrl =>
            Environment.GetEnvironmentVariable("GOTG_FUNCTION_URL") ?? "http://localhost:7234/api/Function1";

        public static async Task<SqlConnection> OpenDatabaseAsync()
        {
            var conn = new SqlConnection(SqlConnectionString);
            try
            {
                await conn.OpenAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not open the gift_of_thegivers database. Make sure LocalDB (localdb)\\ngo is running " +
                    "and SQLQuery1.sql and SQLQuery2_Donations.sql have been run. " + ex.Message, ex);
            }
            return conn;
        }
    }
}
