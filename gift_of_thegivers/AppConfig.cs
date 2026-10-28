namespace gift_of_thegivers
{
    /// <summary>
    /// Addresses the website needs. Locally the defaults are used (LocalDB and the function on port 7234).
    /// On Azure the pipeline sets GOTG_SQL and GOTG_FUNCTION_URL as App Service application settings.
    /// </summary>
    public static class AppConfig
    {
        public static string SqlConnectionString =>
            Environment.GetEnvironmentVariable("GOTG_SQL")
            ?? @"Server=(localdb)\ngo;Database=gift_of_thegivers;Trusted_Connection=True;TrustServerCertificate=True;";

        // On Azure this includes the function key, e.g. https://<app>.azurewebsites.net/api/Function1?code=...
        public static string FunctionUrl =>
            Environment.GetEnvironmentVariable("GOTG_FUNCTION_URL")
            ?? "http://localhost:7234/api/Function1";

        /// <summary>Adds query parameters to the function URL, whether or not it already has ?code=.</summary>
        public static string BuildFunctionUrl(string query)
        {
            string baseUrl = FunctionUrl;
            return baseUrl + (baseUrl.Contains('?') ? "&" : "?") + query;
        }
    }
}
