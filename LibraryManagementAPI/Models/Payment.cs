
using LibraryManagementAPI.Data;

namespace LibraryManagementAPI.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int LoanId { get; set; }
        public Loan Loan { get; set; } = null!;

        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "";
        public DateTime PaymentDate { get; set; }
        public string ReceiptNumber { get; set; } = "";
    }
}
