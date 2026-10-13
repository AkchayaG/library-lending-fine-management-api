
namespace LibraryManagementAPI.Models
{
    public class Loan
    {
        public int Id { get; set; }

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;

        public int MemberId { get; set; }
        public Member Member { get; set; } = null!;

        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        public decimal Fine { get; set; }
        public bool IsReturned { get; set; }
    }
}
