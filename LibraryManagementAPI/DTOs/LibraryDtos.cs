using System.ComponentModel.DataAnnotations;

namespace LibraryManagementAPI.DTOs
{
    public class PaymentDto
    {
        [Range(1, int.MaxValue)]
        public int LoanId { get; set; }

        [Required]
        public string PaymentMethod { get; set; } = "";
    }
}