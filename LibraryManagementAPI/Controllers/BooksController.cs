
using LibraryManagementAPI.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LibraryManagementAPI.Controllers
{
    [ApiController]
    [Route("api/books")]
    public class BooksController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public BooksController(DatabaseConnection db)
        {
            _db = db;
        }

        // POST: Add Book
        [HttpPost]
        public async Task<IActionResult> AddBook(
            [FromBody] BookInput dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) ||
                string.IsNullOrWhiteSpace(dto.Author) ||
                string.IsNullOrWhiteSpace(dto.ISBN) ||
                dto.TotalCopies <= 0)
            {
                return BadRequest("Invalid book details.");
            }

            const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1 FROM dbo.Books WITH (UPDLOCK, HOLDLOCK)
    WHERE ISBN = {{isbn}}
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'ISBN already exists.', 1;
END;

INSERT INTO dbo.Books
(Title, Author, ISBN, PublishedYear,
 IsAvailable, TotalCopies, AvailableCopies)
VALUES
({{title}}, {{author}}, {{isbn}}, {{year}},
 1, {{copies}}, {{copies}});

SELECT SCOPE_IDENTITY() AS NewBookId;

COMMIT TRANSACTION;";

            try
            {
                string result = await _db.ExecuteQueryAsync(
                    sql,
                    ("title", dto.Title.Trim()),
                    ("author", dto.Author.Trim()),
                    ("isbn", dto.ISBN.Trim()),
                    ("year", dto.PublishedYear.ToString()),
                    ("copies", dto.TotalCopies.ToString())
                );

                return Ok(new
                {
                    Message = "Book added successfully",
                    Result = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: View All Books
        [HttpGet]
        public async Task<IActionResult> GetBooks()
        {
            const string sql = @"
SELECT Id, Title, Author, ISBN,
       PublishedYear, IsAvailable,
       TotalCopies, AvailableCopies
FROM dbo.Books
ORDER BY Title
FOR JSON PATH;";

            return await GetJsonResult(sql);
        }

        // GET: Search Books
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest("Enter search keyword.");

            const string sql = @"
SELECT Id, Title, Author, ISBN,
       AvailableCopies
FROM dbo.Books
WHERE Title LIKE '%' + {{keyword}} + '%'
   OR Author LIKE '%' + {{keyword}} + '%'
ORDER BY Title
FOR JSON PATH;";

            return await GetJsonResult(
                sql, ("keyword", keyword.Trim()));
        }

        // GET: Book By ID
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetBook(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid book ID.");

            const string sql = @"
SELECT Id, Title, Author, ISBN,
       PublishedYear, IsAvailable,
       TotalCopies, AvailableCopies
FROM dbo.Books
WHERE Id = {{id}}
FOR JSON PATH;";

            try
            {
                string result = await _db.ExecuteQueryAsync(
                    sql, ("id", id.ToString()));

                var books = ParseJson(result);

                if (books.GetArrayLength() == 0)
                    return NotFound("Book not found.");

                return Ok(books[0].Clone());
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private async Task<IActionResult> GetJsonResult(
            string sql,
            params (string Name, string Value)[] parameters)
        {
            try
            {
                string result =
                    await _db.ExecuteQueryAsync(sql, parameters);

                return Ok(ParseJson(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private static JsonElement ParseJson(string result)
        {
            // sqlcmd can wrap long JSON output in lines.
            string json = string.Concat(
                result.Split('\n')
                    .Select(line => line.TrimEnd('\r')));

            if (string.IsNullOrWhiteSpace(json))
                json = "[]";

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    public class BookInput
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string ISBN { get; set; } = "";
        public int PublishedYear { get; set; }
        public int TotalCopies { get; set; } = 1;
    }
}
