
namespace LibraryManagementAPI.Models
{
    public abstract class LoanBase
    {
        public abstract decimal CalculateFine(int overdueDays);

        public virtual int BorrowingLimit => 3;
        public virtual int LoanDays => 14;
    }

    public class StandardLoan : LoanBase
    {
        public override decimal CalculateFine(int overdueDays)
        {
            return Math.Max(0, overdueDays) * 5m;
        }
    }

    public class PremiumLoan : LoanBase
    {
        public override int BorrowingLimit => 5;
        public override int LoanDays => 21;

        public override decimal CalculateFine(int overdueDays)
        {
            return Math.Max(0, overdueDays) * 2m;
        }
    }

    public readonly record struct BookSummary(
        int BookId,
        string Title,
        int AvailableCopies
    );

    public readonly struct LoanPeriod
    {
        public DateTime Start { get; }
        public DateTime Due { get; }

        public LoanPeriod(DateTime start, DateTime due)
        {
            Start = start;
            Due = due;
        }

        public int Days => (Due - Start).Days;
    }
}
