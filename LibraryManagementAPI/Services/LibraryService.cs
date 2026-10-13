
using LibraryManagementAPI.Models;

namespace LibraryManagementAPI.Services
{
    public class LibraryService : ILibraryService
    {
        public decimal CalculateFine(
            int overdueDays, MembershipType type)
        {
            LoanBase policy = GetPolicy(type);
            return policy.CalculateFine(overdueDays);
        }

        // Method overloading
        public decimal CalculateFine(int overdueDays)
        {
            return CalculateFine(
                overdueDays, MembershipType.Standard);
        }

        public int GetBorrowingLimit(MembershipType type)
        {
            return GetPolicy(type).BorrowingLimit;
        }

        public int GetLoanDays(MembershipType type)
        {
            return GetPolicy(type).LoanDays;
        }

        private static LoanBase GetPolicy(MembershipType type)
        {
            return type switch
            {
                MembershipType.Premium => new PremiumLoan(),
                _ => new StandardLoan()
            };
        }
    }
}
