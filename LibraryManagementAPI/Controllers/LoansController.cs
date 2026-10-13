
using LibraryManagementAPI.Data;
using LibraryManagementAPI.Models;
using LibraryManagementAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace LibraryManagementAPI.Controllers
{
    [ApiController]
    [Route("api/loans")]
    public class LoansController : ControllerBase
    {
        private readonly DatabaseConnection _db;
        private readonly ILibraryService _service;

        public LoansController(
            DatabaseConnection db,
            ILibraryService service)
        {
            _db = db;
            _service = service;
        }

        // POST: api/loans/issue
        [HttpPost("issue")]
        public async Task<IActionResult> IssueBook(
            [FromBody] IssueLoanInput dto)
        {
            if (dto.BookId <= 0 || dto.MemberId <= 0)
                return BadRequest("Invalid book or member ID.");

            try
            {
                const string memberSql = @"
SELECT Membership
FROM dbo.Members
WHERE Id = {{memberId}}
FOR JSON PATH;";

                var memberResult = await _db.ExecuteQueryAsync(
                    memberSql,
                    ("memberId", dto.MemberId.ToString()));

                var members = ParseArray(memberResult);

                if (members.GetArrayLength() == 0)
                    return NotFound("Member not found.");

                int membershipValue = members[0]
                    .GetProperty("Membership").GetInt32();

                var membership = (MembershipType)membershipValue;

                if (!Enum.IsDefined(typeof(MembershipType), membership))
                    return BadRequest("Invalid membership type.");

                int loanDays = _service.GetLoanDays(membership);

                // All checks and updates run in one SQL transaction.
                const string sql = @"
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @BookId INT = CONVERT(INT, {{bookId}});
DECLARE @MemberId INT = CONVERT(INT, {{memberId}});
DECLARE @LoanDays INT = CONVERT(INT, {{loanDays}});
DECLARE @Available INT;
DECLARE @Limit INT;
DECLARE @Active INT;
DECLARE @LoanId INT;

IF NOT EXISTS (
    SELECT 1 FROM dbo.Books
    WHERE Id = @BookId
)
BEGIN
    THROW 50001, 'Book not found.', 1;
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.Members
    WHERE Id = @MemberId
)
BEGIN
    THROW 50002, 'Member not found.', 1;
END;

SELECT @Available = AvailableCopies
FROM dbo.Books WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @BookId;

SELECT @Limit = BorrowingLimit
FROM dbo.Members WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @MemberId;

IF @Available <= 0
BEGIN
    THROW 50003, 'No copies available.', 1;
END;

SELECT @Active = COUNT(*)
FROM dbo.Loans
WHERE MemberId = @MemberId
AND IsReturned = 0;

IF @Active >= @Limit
BEGIN
    THROW 50004, 'Borrowing limit reached.', 1;
END;

DECLARE @IssuedAt DATETIME2 = SYSUTCDATETIME();

INSERT INTO dbo.Loans
(BookId, MemberId, IssueDate, DueDate,
 IsReturned, Fine)
VALUES
(@BookId, @MemberId, @IssuedAt,
 DATEADD(DAY, @LoanDays, @IssuedAt), 0, 0);

SET @LoanId = CONVERT(INT, SCOPE_IDENTITY());

UPDATE dbo.Books
SET AvailableCopies = AvailableCopies - 1,
    IsAvailable = CASE
        WHEN AvailableCopies - 1 > 0 THEN 1
        ELSE 0
    END
WHERE Id = @BookId;

COMMIT TRANSACTION;

SELECT l.Id AS LoanId,
       b.Title AS Book,
       m.Name AS Member,
       l.IssueDate,
       l.DueDate,
       b.AvailableCopies
FROM dbo.Loans l
JOIN dbo.Books b ON l.BookId = b.Id
JOIN dbo.Members m ON l.MemberId = m.Id
WHERE l.Id = @LoanId
FOR JSON PATH;";

                string result = await _db.ExecuteQueryAsync(
                    sql,
                    ("bookId", dto.BookId.ToString()),
                    ("memberId", dto.MemberId.ToString()),
                    ("loanDays", loanDays.ToString())
                );

                var loans = ParseArray(result);

                return Ok(new
                {
                    Message = "Book issued successfully",
                    Loan = loans[0].Clone()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/loans/return
        [HttpPost("return")]
        public async Task<IActionResult> ReturnBook(
            [FromBody] ReturnLoanInput dto)
        {
            if (dto.LoanId <= 0)
                return BadRequest("Invalid loan ID.");

            try
            {
                const string lookupSql = @"
SELECT l.Id, l.IsReturned,
       l.DueDate, m.Membership
FROM dbo.Loans l
JOIN dbo.Members m ON l.MemberId = m.Id
WHERE l.Id = {{loanId}}
FOR JSON PATH;";

                string lookup = await _db.ExecuteQueryAsync(
                    lookupSql,
                    ("loanId", dto.LoanId.ToString()));

                var loans = ParseArray(lookup);

                if (loans.GetArrayLength() == 0)
                    return NotFound("Loan not found.");

                var loan = loans[0];

                if (loan.GetProperty("IsReturned").GetBoolean())
                    return BadRequest("Book already returned.");

                DateTime dueDate = DateTime.Parse(
                    loan.GetProperty("DueDate").GetString()!,
                    CultureInfo.InvariantCulture);

                int overdueDays = Math.Max(
                    0,
                    (DateTime.UtcNow.Date - dueDate.Date).Days);

                var membership = (MembershipType)loan
                    .GetProperty("Membership").GetInt32();

                if (!Enum.IsDefined(typeof(MembershipType), membership))
                    return BadRequest("Invalid membership type.");

                decimal fine = _service.CalculateFine(
                    overdueDays, membership);

                const string sql = @"
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @LoanId INT = CONVERT(INT, {{loanId}});
DECLARE @Fine DECIMAL(18,2) =
    CONVERT(DECIMAL(18,2), {{fine}});
DECLARE @BookId INT;

SELECT @BookId = BookId
FROM dbo.Loans WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @LoanId AND IsReturned = 0;

IF @BookId IS NULL
BEGIN
    THROW 50005, 'Loan not found or already returned.', 1;
END;

UPDATE dbo.Loans
SET IsReturned = 1,
    ReturnDate = SYSUTCDATETIME(),
    Fine = @Fine
WHERE Id = @LoanId;

UPDATE dbo.Books
SET AvailableCopies = AvailableCopies + 1,
    IsAvailable = 1
WHERE Id = @BookId;

COMMIT TRANSACTION;

SELECT l.Id AS LoanId,
       b.Title AS Book,
       l.Fine,
       b.AvailableCopies
FROM dbo.Loans l
JOIN dbo.Books b ON l.BookId = b.Id
WHERE l.Id = @LoanId
FOR JSON PATH;";

                string result = await _db.ExecuteQueryAsync(
                    sql,
                    ("loanId", dto.LoanId.ToString()),
                    ("fine", fine.ToString(CultureInfo.InvariantCulture))
                );

                var returned = ParseArray(result);

                return Ok(new
                {
                    Message = "Book returned successfully",
                    OverdueDays = overdueDays,
                    Details = returned[0].Clone()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/loans
        [HttpGet]
        public async Task<IActionResult> GetLoans()
        {
            const string sql = @"
SELECT l.Id,
       b.Title AS Book,
       m.Name AS Member,
       l.IssueDate,
       l.DueDate,
       l.ReturnDate,
       l.IsReturned,
       l.Fine
FROM dbo.Loans l
JOIN dbo.Books b ON l.BookId = b.Id
JOIN dbo.Members m ON l.MemberId = m.Id
ORDER BY l.IssueDate DESC
FOR JSON PATH;";

            try
            {
                string result = await _db.ExecuteQueryAsync(sql);
                return Ok(ParseArray(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private static JsonElement ParseArray(string output)
        {
            string json = string.Concat(
                output.Split('\n')
                    .Select(line => line.TrimEnd('\r')));

            if (string.IsNullOrWhiteSpace(json))
                json = "[]";

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    public class IssueLoanInput
    {
        public int BookId { get; set; }
        public int MemberId { get; set; }
    }

    public class ReturnLoanInput
    {
        public int LoanId { get; set; }
    }
}
