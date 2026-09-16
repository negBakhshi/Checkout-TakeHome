using FluentValidation;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

public class PostPaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    private static readonly HashSet<string> IsoCurrencyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP",
        "USD",
        "EUR",
    };

    public PostPaymentRequestValidator(TimeProvider timeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .WithMessage("Card number is required.")
            .Length(14, 19)
            .WithMessage("Card number must be between 14 and 19 characters long.")
            .Matches("^[0-9]+$")
            .WithMessage("Card number must only contain numeric characters.");

        RuleFor(x => x.ExpiryMonth)
            .NotNull()
            .WithMessage("Expiry month is required.")
            .InclusiveBetween(1, 12)
            .WithMessage("Expiry month must be between 1 and 12.");

        RuleFor(x => x.ExpiryYear).NotNull().WithMessage("Expiry year is required.");

        RuleFor(x => x)
            .Must(x => IsExpiryInFuture(x.ExpiryMonth!.Value, x.ExpiryYear!.Value, timeProvider))
            .When(x => x.ExpiryMonth is >= 1 and <= 12 && x.ExpiryYear is not null)
            .WithMessage("Card expiry date must be in the future.")
            .WithName("Expiry");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency must be 3 characters long.")
            .Must(IsoCurrencyCodes.Contains)
            .WithMessage("Currency is not supported.");

        RuleFor(x => x.Amount)
            .NotNull()
            .WithMessage("Amount is required.")
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.Cvv)
            .NotEmpty()
            .WithMessage("CVV is required.")
            .Length(3, 4)
            .WithMessage("CVV must be between 3 and 4 characters long.")
            .Matches("^[0-9]+$")
            .WithMessage("CVV must only contain numeric characters.");
    }

    private static bool IsExpiryInFuture(int month, int year, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();

        return year > now.Year || (year == now.Year && month >= now.Month);
    }
}
