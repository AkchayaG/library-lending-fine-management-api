
using LibraryManagementAPI.Data;
using LibraryManagementAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LibraryManagementAPI.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public PaymentsController(DatabaseConnection db)
        {
            _db = db;
        }

        // POST: api/payments
        [HttpPost]
        public async Task<IActionResult> PayFine(
            [FromBody] FinePaymentInput dto)
        {
            if (dto.LoanId <= 0)
                return BadRequest("Invalid loan ID.");

            string method = (dto.PaymentMethod ?? "")
                .Trim().ToLowerInvariant();

            IPaymentProcessor processor;

            switch (method)
            {
                case "cash":
                    processor = new CashPaymentProcessor();
                    break;

                case "digital":
                    processor = new DigitalPaymentProcessor();
                    break;

                default:
                    return BadRequest(
                        "Payment method must be cash or digital.");
            }

            try
            {
                const string lookupSql = @"
SELECT Id, Fine, IsReturned
FROM dbo.Loans
WHERE Id = {{loanId}}
FOR JSON PATH;";

                string lookup = await _db.ExecuteQueryAsync(
                    lookupSql,
                    ("loanId", dto.LoanId.ToString()));

                var loans = ParseArray(lookup);

                if (loans.GetArrayLength() == 0)
                    return NotFound("Loan not found.");

                var loan = loans[0];

                if (!loan.GetProperty("IsReturned").GetBoolean())
                    return BadRequest("Return the book first.");

                decimal fine = loan.GetProperty("Fine").GetDecimal();

                if (fine <= 0)
                    return BadRequest("No fine to pay.");

                if (!processor.ProcessPayment(fine))
                    return BadRequest("Payment failed.");

                string receipt = "REC-" +
                    Guid.NewGuid().ToString("N")[..10]
                    .ToUpperInvariant();

                const string paymentSql = @"
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @LoanId INT = CONVERT(INT, {{loanId}});
DECLARE @PaymentId INT;
DECLARE @Amount DECIMAL(18,2);

SELECT @Amount = Fine
FROM dbo.Loans WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @LoanId AND IsReturned = 1;

IF @Amount IS NULL
    THROW 50001, 'Loan not returned or not found.', 1;

IF @Amount <= 0
    THROW 50002, 'No fine to pay.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
    WHERE LoanId = @LoanId
)
    THROW 50003, 'Fine already paid.', 1;

INSERT INTO dbo.Payments
(
    LoanId,
    Amount,
    PaymentMethod,
    PaymentDate,
    ReceiptNumber
)
VALUES
(
    @LoanId,
    @Amount,
    {{method}},
    SYSUTCDATETIME(),
    {{receipt}}
);

SET @PaymentId = CONVERT(INT, SCOPE_IDENTITY());

COMMIT TRANSACTION;

SELECT
    Id,
    ReceiptNumber,
    LoanId,
    Amount,
    PaymentMethod,
    PaymentDate
FROM dbo.Payments
WHERE Id = @PaymentId
FOR JSON PATH;";

                string result = await _db.ExecuteQueryAsync(
                    paymentSql,
                    ("loanId", dto.LoanId.ToString()),
                    ("method", method),
                    ("receipt", receipt));

                var payments = ParseArray(result);

                return Ok(new
                {
                    Message = "Simulated payment successful",
                    Payment = payments[0].Clone()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/payments/1
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetReceipt(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid payment ID.");

            const string sql = @"
SELECT
    p.ReceiptNumber,
    m.Name AS Member,
    p.Amount,
    p.PaymentMethod,
    p.PaymentDate
FROM dbo.Payments p
JOIN dbo.Loans l ON p.LoanId = l.Id
JOIN dbo.Members m ON l.MemberId = m.Id
WHERE p.Id = {{id}}
FOR JSON PATH;";

            try
            {
                string result = await _db.ExecuteQueryAsync(
                    sql,
                    ("id", id.ToString()));

                var receipts = ParseArray(result);

                if (receipts.GetArrayLength() == 0)
                    return NotFound("Receipt not found.");

                return Ok(new
                {
                    Receipt = receipts[0].Clone(),
                    Status = "Paid (Simulation)"
                });
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

    public class FinePaymentInput
    {
        public int LoanId { get; set; }

        public string PaymentMethod { get; set; } = "";
    }
}
