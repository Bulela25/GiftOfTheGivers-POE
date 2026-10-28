using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace givers_function
{
    /// <summary>
    /// Section C.3 - applies Database/schema.sql to the Azure SQL Database.
    /// The deployment pipeline calls this right after the function is deployed.
    /// It signs in to SQL with the Function App's managed identity (no password),
    /// and the script only applies versions that have not been applied before.
    /// Protected by a function key, so only the pipeline can call it.
    /// </summary>
    public class ApplySchema
    {
        private readonly ILogger<ApplySchema> _logger;

        public ApplySchema(ILogger<ApplySchema> logger)
        {
            _logger = logger;
        }

        [Function("ApplySchema")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            string script;
            using (Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("schema.sql"))
            {
                if (stream == null)
                {
                    return new ObjectResult("schema.sql was not found in the function package.") { StatusCode = 500 };
                }
                using var reader = new StreamReader(stream);
                script = await reader.ReadToEndAsync();
            }

            // SQL scripts use GO to separate batches; SqlCommand needs them run one at a time
            List<string> batches = Regex
                .Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(HasSql)
                .ToList();

            var log = new StringBuilder();
            log.AppendLine($"Schema update started {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ({batches.Count} batches)");

            try
            {
                using var conn = new SqlConnection(Function1.ConnectionString);
                conn.InfoMessage += (_, e) => log.AppendLine("  " + e.Message);   // the script's PRINT lines
                await conn.OpenAsync();
                log.AppendLine($"Connected to database: {conn.Database}");

                foreach (string batch in batches)
                {
                    using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
                    await cmd.ExecuteNonQueryAsync();
                }

                log.AppendLine();
                log.AppendLine("Applied schema versions:");
                using var list = new SqlCommand(
                    "SELECT Version, Description, AppliedOn FROM dbo.SchemaVersions ORDER BY Version", conn);
                using var rows = await list.ExecuteReaderAsync();
                while (await rows.ReadAsync())
                {
                    log.AppendLine($"  v{rows.GetInt32(0)}  {rows.GetString(1),-40}  applied {rows.GetDateTime(2):yyyy-MM-dd HH:mm} UTC");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schema update failed");
                log.AppendLine("FAILED: " + ex.Message);
                return new ObjectResult(log.ToString()) { StatusCode = 500 };
            }

            _logger.LogInformation("Schema update finished");
            return new OkObjectResult(log.ToString());
        }

        // Skip batches that are empty or only comments
        private static bool HasSql(string batch) =>
            batch.Split('\n')
                 .Select(line => line.Trim())
                 .Any(line => line.Length > 0 && !line.StartsWith("--"));
    }
}
