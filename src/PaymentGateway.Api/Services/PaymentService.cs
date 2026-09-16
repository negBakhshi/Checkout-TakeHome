using PaymentGateway.Api.BankSimulator;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentService(IBankClient bankClient, IPaymentsRepository paymentsRepository)
    : IPaymentService
{
    public async Task<PostPaymentResponse> PostPaymentAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken
    )
    {
        // The validator is checking for the nullability, so it is safe here to use the null-forgiving operator.
        var cardNumber = request.CardNumber!;
        var expiryMonth = request.ExpiryMonth!.Value;
        var expiryYear = request.ExpiryYear!.Value;
        var currency = request.Currency!.ToUpperInvariant();
        var amount = request.Amount!.Value;

        var bankRequest = new BankPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryDate = $"{expiryMonth:D2}/{expiryYear}",
            Currency = currency,
            Amount = amount,
            Cvv = request.Cvv!,
        };

        var bankResponse = await bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = cardNumber[^4..],
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = bankRequest.Currency,
            Amount = amount,
        };

        paymentsRepository.Add(payment);
        return payment;
    }
}
