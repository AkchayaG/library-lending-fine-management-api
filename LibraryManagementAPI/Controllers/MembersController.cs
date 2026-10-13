
using LibraryManagementAPI.Data;
using LibraryManagementAPI.Models;
using LibraryManagementAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LibraryManagementAPI.Controllers
{
    [ApiController]
    [Route("api/members")]
    public class MembersController : ControllerBase
    {
        private readonly DatabaseConnection _db;
        private readonly ILibraryService _service;

        public MembersController(
            DatabaseConnection db,
            ILibraryService service)
        {
            _db = db;
            _service = service;
        }

        // POST: api/members
        [HttpPost]
        public async Task<IActionResult> Register(
            [FromBody] RegisterMemberInput dto)
        {
            if (dto.Id <= 0 ||
                string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Invalid member details.");
            }

            if (!Enum.IsDefined(
                typeof(MembershipType), dto.Membership))
            {
                return BadRequest("Invalid membership type.");
            }

            int limit = _service.GetBorrowingLimit(
                dto.Membership);

            const string sql = @"
IF EXISTS (
    SELECT 1 FROM dbo.Members
    WHERE Id = {{id}}
)
BEGIN
    THROW 50001, 'Member ID already exists.', 1;
END;

INSERT INTO dbo.Members
(Id, Name, Membership, BorrowingLimit)
VALUES
({{id}}, {{name}}, {{membership}}, {{limit}});";

            try
            {
                await _db.ExecuteQueryAsync(
                    sql,
                    ("id", dto.Id.ToString()),
                    ("name", dto.Name.Trim()),
                    ("membership", ((int)dto.Membership).ToString()),
                    ("limit", limit.ToString())
                );

                return Ok(new
                {
                    dto.Id,
                    Name = dto.Name.Trim(),
                    Membership = dto.Membership.ToString(),
                    BorrowingLimit = limit,
                    Message = "Member registered successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/members
        [HttpGet]
        public async Task<IActionResult> GetMembers()
        {
            const string sql = @"
SELECT Id, Name, Membership, BorrowingLimit
FROM dbo.Members
ORDER BY Id
FOR JSON PATH;";

            try
            {
                string result = await _db.ExecuteQueryAsync(sql);

                string json = string.Concat(
                    result.Split('\n')
                        .Select(line => line.TrimEnd('\r')));

                if (string.IsNullOrWhiteSpace(json))
                    json = "[]";

                using var document = JsonDocument.Parse(json);

                return Ok(document.RootElement.Clone());
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public class RegisterMemberInput
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public MembershipType Membership { get; set; }
    }
}
