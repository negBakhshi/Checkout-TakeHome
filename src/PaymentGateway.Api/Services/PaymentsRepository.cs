using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly List<PostPaymentResponse> Payments = [];

    public void Add(PostPaymentResponse payment)
    {
        Payments.Add(payment);
    }

    public PostPaymentResponse? Get(Guid id)
    {
        return Payments.FirstOrDefault(p => p.Id == id);
    }
}
