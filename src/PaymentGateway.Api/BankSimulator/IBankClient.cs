using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.BankSimulator;

public interface IBankClient
{
    Task<BankResponse> ProcessPaymentAsync(
        BankPaymentRequest request,
        CancellationToken cancellationToken
    );
}

