using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using PaymentGateway.Api.BankSimulator;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Extentions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// Runs the real pipeline (routing, binding, validator, service, in-memory repository)
/// with only the bank swapped for a mock. Field-level validation is covered in
/// <see cref="PostPaymentRequestValidatorTests"/>; here we only care about each HTTP outcome.
/// </summary>
public class PaymentsControllerTests : IDisposable
{
    private readonly Mock<IBankClient> _bankClient = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PaymentsControllerTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton(_bankClient.Object));
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(TestData.CreateTimeProvider()));
            })
        );
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private void BankReturns(bool authorized) =>
        _bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankResponse { Authorized = authorized });

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task Post_ValidRequest_ReturnsBankDecision(bool authorized, PaymentStatus expected)
    {
        BankReturns(authorized);

        var response = await _client.PostAsJsonAsync("/api/payments", TestData.ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.NotNull(body);
        Assert.Equal(expected, body.Status);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("8877", body.CardNumberLastFour);
    }

    [Fact]
    public async Task Post_InvalidRequest_ReturnsRejectedWithoutCallingBank()
    {
        var response = await _client.PostAsJsonAsync("/api/payments", TestData.ValidRequest(cardNumber: "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RejectedPaymentResponse>();
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Rejected, body.Status);
        Assert.NotEmpty(body.Errors);
        _bankClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_BankUnavailable_Returns503()
    {
        _bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BankUnavailableException("Bank is currently unavailable"));

        var response = await _client.PostAsJsonAsync("/api/payments", TestData.ValidRequest());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Acquiring bank unavailable", problem?.Title);
    }

    [Fact]
    public async Task Post_ThenGet_ReturnsStoredPayment()
    {
        BankReturns(authorized: true);

        var post = await _client.PostAsJsonAsync("/api/payments", TestData.ValidRequest());
        var created = await post.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.NotNull(created);

        var get = await _client.GetAsync($"/api/payments/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<GetPaymentResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Status, fetched.Status);
        Assert.Equal(created.CardNumberLastFour, fetched.CardNumberLastFour);
        Assert.Equal(created.ExpiryMonth, fetched.ExpiryMonth);
        Assert.Equal(created.ExpiryYear, fetched.ExpiryYear);
        Assert.Equal(created.Currency, fetched.Currency);
        Assert.Equal(created.Amount, fetched.Amount);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
