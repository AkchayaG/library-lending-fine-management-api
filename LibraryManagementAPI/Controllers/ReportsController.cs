
using LibraryManagementAPI.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LibraryManagementAPI.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public ReportsController(DatabaseConnection db)
        {
            _db = db;
        }

        // GET: api/reports
        [HttpGet]
        public async Task<IActionResult> GetReport()
        {
            const string sql = @"
SELECT
    JSON_QUERY((
        SELECT TOP 5
            b.Id AS BookId,
            b.Title,
            COUNT(*) AS BorrowCount
        FROM dbo.Loans l
        JOIN dbo.Books b ON l.BookId = b.Id
        GROUP BY b.Id, b.Title
        ORDER BY COUNT(*) DESC, b.Title
        FOR JSON PATH
    )) AS MostBorrowedBooks,

    (SELECT COALESCE(SUM(Fine), 0)
     FROM dbo.Loans) AS TotalFines,

    (SELECT COALESCE(SUM(Amount), 0)
     FROM dbo.Payments) AS PaidFines,

    (SELECT COALESCE(SUM(Fine), 0)
     FROM dbo.Loans)
    -
    (SELECT COALESCE(SUM(Amount), 0)
     FROM dbo.Payments) AS OutstandingFines

FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;";

            try
            {
                string result = await _db.ExecuteQueryAsync(sql);

                string json = string.Concat(
                    result.Split('\n')
                        .Select(line => line.TrimEnd('\r')));

                using var document = JsonDocument.Parse(json);

                return Ok(document.RootElement.Clone());
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
