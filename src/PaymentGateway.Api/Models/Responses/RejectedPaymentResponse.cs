using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models.Responses;

public class RejectedPaymentResponse
{
    public PaymentStatus Status { get; init; } = PaymentStatus.Rejected;
    public IReadOnlyList<string> Errors { get; init; } = [];
}

