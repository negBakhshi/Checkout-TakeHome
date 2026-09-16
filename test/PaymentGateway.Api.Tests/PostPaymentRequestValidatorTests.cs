using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests;

public class PostPaymentRequestValidatorTests
{
    private readonly PostPaymentRequestValidator _validator = new(TestData.CreateTimeProvider());

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        _validator.TestValidate(TestData.ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "Card number is required.")]
    [InlineData("12345678901234567890", "Card number must be between 14 and 19 characters long.")]
    [InlineData("2222 4053 4324 8877", "Card number must only contain numeric characters.")]
    [InlineData("12345678901234", null)]
    public void CardNumber_Validation(string? value, string? expectedError)
    {
        var result = _validator.TestValidate(TestData.ValidRequest(cardNumber: value));

        AssertField(result, x => x.CardNumber, expectedError);
    }

    [Theory]
    [InlineData(null, "Expiry month is required.")]
    [InlineData(0, "Expiry month must be between 1 and 12.")]
    [InlineData(13, "Expiry month must be between 1 and 12.")]
    [InlineData(12, null)]
    public void ExpiryMonth_Validation(int? value, string? expectedError)
    {
        var result = _validator.TestValidate(TestData.ValidRequest(expiryMonth: value));

        AssertField(result, x => x.ExpiryMonth, expectedError);
        // A missing or out-of-range month should not also be reported as expired.
        result.ShouldNotHaveValidationErrorFor("Expiry");
    }

    [Fact]
    public void ExpiryYear_Missing_IsRequired()
    {
        var result = _validator.TestValidate(TestData.ValidRequest(expiryYear: null));

        result
            .ShouldHaveValidationErrorFor(x => x.ExpiryYear)
            .WithErrorMessage("Expiry year is required.");
        result.ShouldNotHaveValidationErrorFor("Expiry");
    }

    // "Now" is June 2026. A card is valid through the end of its expiry month.
    [Theory]
    [InlineData(12, 2025, false)]
    [InlineData(5, 2026, false)]
    [InlineData(6, 2026, true)]
    [InlineData(7, 2026, true)]
    [InlineData(1, 2027, true)]
    public void Expiry_MustBeCurrentMonthOrLater(int month, int year, bool valid)
    {
        var result = _validator.TestValidate(
            TestData.ValidRequest(expiryMonth: month, expiryYear: year)
        );

        if (valid)
            result.ShouldNotHaveValidationErrorFor("Expiry");
        else
            result
                .ShouldHaveValidationErrorFor("Expiry")
                .WithErrorMessage("Card expiry date must be in the future.");
    }

    // Same rule evaluated from different points in time, to show it depends on the
    // injected clock rather than the real calendar.
    [Theory]
    [InlineData(2023, 3, 2024, 1, true)]
    [InlineData(2023, 3, 2023, 3, true)]
    [InlineData(2023, 3, 2023, 2, false)]
    [InlineData(2031, 11, 2031, 10, false)]
    public void Expiry_IsRelativeToClock(
        int nowYear,
        int nowMonth,
        int expiryYear,
        int expiryMonth,
        bool valid
    )
    {
        var clock = new FakeTimeProvider(
            new DateTimeOffset(nowYear, nowMonth, 15, 0, 0, 0, TimeSpan.Zero)
        );
        var validator = new PostPaymentRequestValidator(clock);

        var result = validator.TestValidate(
            TestData.ValidRequest(expiryMonth: expiryMonth, expiryYear: expiryYear)
        );

        if (valid)
            result.ShouldNotHaveValidationErrorFor("Expiry");
        else
            result.ShouldHaveValidationErrorFor("Expiry");
    }

    [Theory]
    [InlineData(null, "Currency is required.")]
    [InlineData("GBPP", "Currency must be 3 characters long.")]
    [InlineData("JPY", "Currency is not supported.")]
    [InlineData("GBP", null)]
    [InlineData("gbp", null)] // accepted; normalised to upper-case by the service
    public void Currency_Validation(string? value, string? expectedError)
    {
        var result = _validator.TestValidate(TestData.ValidRequest(currency: value));

        AssertField(result, x => x.Currency, expectedError);
    }

    [Theory]
    [InlineData(null, "Amount is required.")]
    [InlineData(0, "Amount must be greater than 0.")]
    [InlineData(-1, "Amount must be greater than 0.")]
    [InlineData(1, null)]
    public void Amount_Validation(int? value, string? expectedError)
    {
        var result = _validator.TestValidate(TestData.ValidRequest(amount: value));

        AssertField(result, x => x.Amount, expectedError);
    }

    [Theory]
    [InlineData(null, "CVV is required.")]
    [InlineData("12345", "CVV must be between 3 and 4 characters long.")]
    [InlineData("12a", "CVV must only contain numeric characters.")]
    [InlineData("012", null)] // leading zero must survive, hence CVV is a string
    [InlineData("1234", null)]
    public void Cvv_Validation(string? value, string? expectedError)
    {
        var result = _validator.TestValidate(TestData.ValidRequest(cvv: value));

        AssertField(result, x => x.Cvv, expectedError);
    }

    private static void AssertField<TProperty>(
        TestValidationResult<Models.Requests.PostPaymentRequest> result,
        System.Linq.Expressions.Expression<
            Func<Models.Requests.PostPaymentRequest, TProperty>
        > property,
        string? expectedError
    )
    {
        if (expectedError is null)
            result.ShouldNotHaveValidationErrorFor(property);
        else
            result.ShouldHaveValidationErrorFor(property).WithErrorMessage(expectedError);
    }
}
