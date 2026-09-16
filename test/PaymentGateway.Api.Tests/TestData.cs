using Microsoft.Extensions.Time.Testing;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// Shared builders so each test only spells out the field it cares about.
/// </summary>
internal static class TestData
{
    private const int NowYear = 2026;
    private const int NowMonth = 6;
    public static readonly DateTimeOffset Now = new(NowYear, NowMonth, 15, 12, 0, 0, TimeSpan.Zero);

    public const int DefaultExpiryMonth = 12;
    public const int DefaultExpiryYear = NowYear + 1;

    // Card ending in an odd digit is authorised by the bank simulator.
    public const string AuthorizedCardNumber = "2222405343248877";

    // Card ending in an even digit is declined by the bank simulator.
    public const string DeclinedCardNumber = "2222405343248878";

    // FakeTimeProvider is mutable, so eachtest gets its own instance rather than sharing one across parallel test classes.
    public static FakeTimeProvider CreateTimeProvider() => new(Now);

    public static PostPaymentRequest ValidRequest(
        string? cardNumber = AuthorizedCardNumber,
        int? expiryMonth = DefaultExpiryMonth,
        int? expiryYear = DefaultExpiryYear,
        string? currency = "GBP",
        int? amount = 1050,
        string? cvv = "123"
    ) =>
        new()
        {
            CardNumber = cardNumber,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            Cvv = cvv,
        };
}

