using Moq;
using PaymentGateway.Api.BankSimulator;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Extentions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// Unit tests for the orchestration layer. The bank and the repository are mocked
/// so these only exercise the mapping and decision logic in <see cref="PaymentService"/>.
/// </summary>
public class PaymentServiceTests
{
    private readonly Mock<IBankClient> _bankClient = new();
    private readonly Mock<IPaymentsRepository> _repository = new();
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _sut = new PaymentService(_bankClient.Object, _repository.Object);
    }

    private void BankReturns(bool authorized) =>
        _bankClient
            .Setup(b =>
                b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new BankResponse
                {
                    Authorized = authorized,
                    AuthorizationCode = authorized ? Guid.NewGuid().ToString() : "",
                }
            );

    [Fact]
    public async Task BankAuthorises_ReturnsAuthorizedAndStoresPayment()
    {
        BankReturns(authorized: true);

        var result = await _sut.PostPaymentAsync(TestData.ValidRequest(), CancellationToken.None);

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        _repository.Verify(r => r.Add(result), Times.Once);
    }

    [Fact]
    public async Task BankDeclines_ReturnsDeclinedAndStoresPayment()
    {
        BankReturns(authorized: false);

        var result = await _sut.PostPaymentAsync(TestData.ValidRequest(), CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, result.Status);
        _repository.Verify(r => r.Add(result), Times.Once);
    }

    [Fact]
    public async Task MapsRequestToBankContract()
    {
        BankPaymentRequest? sent = null;
        _bankClient
            .Setup(b =>
                b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<BankPaymentRequest, CancellationToken>((req, _) => sent = req)
            .ReturnsAsync(new BankResponse { Authorized = true, AuthorizationCode = "abc" });

        var request = TestData.ValidRequest(
            expiryMonth: 3,
            expiryYear: 2028,
            currency: "gbp",
            amount: 999,
            cvv: "012"
        );

        await _sut.PostPaymentAsync(request, CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal(request.CardNumber, sent.CardNumber);
        Assert.Equal("03/2028", sent.ExpiryDate); // zero-padded MM/YYYY
        Assert.Equal("GBP", sent.Currency); // normalised to upper-case
        Assert.Equal(999, sent.Amount); // minor units passed through untouched
        Assert.Equal("012", sent.Cvv); // leading zero preserved
    }

    [Fact]
    public async Task ResponseNeverContainsFullCardNumberOrCvv()
    {
        BankReturns(authorized: true);
        var request = TestData.ValidRequest(cardNumber: "4111111111110042");

        var result = await _sut.PostPaymentAsync(request, CancellationToken.None);

        Assert.Equal("0042", result.CardNumberLastFour);
        // PostPaymentResponse has no CardNumber / Cvv property, so this is enforced by the type.
        Assert.DoesNotContain(
            typeof(PostPaymentResponse).GetProperties(),
            p => p.Name is "CardNumber" or "Cvv"
        );
    }

    [Fact]
    public async Task BankUnavailable_PropagatesAndStoresNothing()
    {
        _bankClient
            .Setup(b =>
                b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new BankUnavailableException("down"));

        await Assert.ThrowsAsync<BankUnavailableException>(() =>
            _sut.PostPaymentAsync(TestData.ValidRequest(), CancellationToken.None)
        );

        // No decision was made, so nothing should be persisted.
        _repository.Verify(r => r.Add(It.IsAny<PostPaymentResponse>()), Times.Never);
    }

    [Fact]
    public async Task PassesCancellationTokenToBank()
    {
        using var cts = new CancellationTokenSource();
        BankReturns(authorized: true);

        await _sut.PostPaymentAsync(TestData.ValidRequest(), cts.Token);

        _bankClient.Verify(
            b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), cts.Token),
            Times.Once
        );
    }
}

