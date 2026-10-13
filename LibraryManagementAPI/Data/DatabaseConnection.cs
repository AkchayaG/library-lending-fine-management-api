
using System.Diagnostics;

namespace LibraryManagementAPI.Data
{
    public class DatabaseConnection
    {
        private readonly string _server = @".\SQLEXPRESS";
        private readonly string _database = "LibraryManagementDB";

        public async Task<string> ExecuteQueryAsync(
            string query,
            params (string Name, string Value)[] parameters)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sqlcmd",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            // SQL Server connection arguments
            foreach (var arg in new[]
            {
                "-S", _server,
                "-E",
                "-d", _database,
                "-b",
                "-w", "65535",
                "-y", "0",
                "-x"
            })
            {
                startInfo.ArgumentList.Add(arg);
            }

            // Local learning-project workaround.
            // This is not true SQL parameterization.
            foreach (var parameter in parameters)
            {
                string literal = "N'" +
                    parameter.Value.Replace("'", "''") + "'";

                query = query.Replace(
                    "{{" + parameter.Name + "}}",
                    literal);
            }

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            await process.StandardInput.WriteLineAsync(
                "SET NOCOUNT ON;");

            await process.StandardInput.WriteLineAsync(query);

            await process.StandardInput.WriteLineAsync("GO");

            process.StandardInput.Close();

            var outputTask =
                process.StandardOutput.ReadToEndAsync();

            var errorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    "SQL Server Error: " + error);
            }

            // Remove sqlcmd column headers and separators.
            var lines = output.Split('\n')
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith("---") &&
                    !line.StartsWith("Changed database context"))
                .ToList();

            // FOR JSON PATH queries return JSON text.
            // For JSON results, keep only the JSON content.
            int jsonStart = lines.FindIndex(line =>
                line.StartsWith("[") || line.StartsWith("{"));

            if (jsonStart >= 0)
            {
                return string.Concat(lines.Skip(jsonStart));
            }

            return string.Join("\n", lines);
        }
    }
}
