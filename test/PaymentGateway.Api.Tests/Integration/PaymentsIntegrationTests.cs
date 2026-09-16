using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.Integration;

[Trait("Category", "Integration")]
public class PaymentsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PaymentsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CardEndingInOddDigit_IsAuthorised_AndRetrievable()
    {
        var post = await _client.PostAsJsonAsync(
            "/api/payments",
            TestData.ValidRequest(cardNumber: "2222405343248877")
        );

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<PostPaymentResponse>(TestData.Json);
        Assert.NotNull(created);
        Assert.Equal(PaymentStatus.Authorized, created.Status);
        Assert.Equal("8877", created.CardNumberLastFour);

        var get = await _client.GetAsync($"/api/payments/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<GetPaymentResponse>(TestData.Json);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(PaymentStatus.Authorized, fetched.Status);
    }

    [Fact]
    public async Task CardEndingInEvenDigit_IsDeclined()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            TestData.ValidRequest(cardNumber: "2222405343248878")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(TestData.Json);
        Assert.Equal(PaymentStatus.Declined, body?.Status);
    }

    [Fact]
    public async Task CardEndingInZero_BankReturns503_GatewayReturns503()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            TestData.ValidRequest(cardNumber: "2222405343248870")
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task InvalidRequest_IsRejectedBeforeReachingBank()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            TestData.ValidRequest(expiryYear: 2020)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RejectedPaymentResponse>(TestData.Json);
        Assert.Equal(PaymentStatus.Rejected, body?.Status);
    }
}
