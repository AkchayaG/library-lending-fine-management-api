
using LibraryManagementAPI.Models;

namespace LibraryManagementAPI.Services
{
    public interface ILibraryService
    {
        decimal CalculateFine(int overdueDays, MembershipType type);
        int GetBorrowingLimit(MembershipType type);
        int GetLoanDays(MembershipType type);
    }
}
