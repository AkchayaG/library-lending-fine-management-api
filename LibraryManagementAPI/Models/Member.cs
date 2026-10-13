
namespace LibraryManagementAPI.Models
{
    public enum MembershipType
    {
        Standard,
        Premium
    }

    public class Member
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public MembershipType Membership { get; set; }
        public int BorrowingLimit { get; set; }

        public List<Loan> Loans { get; set; } = new();
    }
}
