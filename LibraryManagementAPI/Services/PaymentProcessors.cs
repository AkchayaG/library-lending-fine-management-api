
namespace LibraryManagementAPI.Services
{
    public interface IPaymentProcessor
    {
        bool ProcessPayment(decimal amount);
    }

    public class CashPaymentProcessor : IPaymentProcessor
    {
        public bool ProcessPayment(decimal amount)
        {
            return amount > 0;
        }
    }

    public class DigitalPaymentProcessor : IPaymentProcessor
    {
        public bool ProcessPayment(decimal amount)
        {
            return amount > 0;
        }
    }

}
