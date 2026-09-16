using System.Net;
using System.Net.Http.Json;
using PaymentGateway.Api.BankSimulator;
using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests;

public class BankClientTests
{
    private static readonly BankPaymentRequest AnyRequest = new()
    {
        CardNumber = TestData.AuthorizedCardNumber,
        ExpiryDate = "12/2027",
        Currency = "GBP",
        Amount = 100,
        Cvv = "123",
    };

    private static BankClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond
    ) =>
        new(
            new HttpClient(new StubHandler(respond))
            {
                BaseAddress = new Uri("http://bank.test"),
                Timeout = TimeSpan.FromMilliseconds(200),
            }
        );

    [Fact]
    public async Task Ok_Authorised_ReturnsParsedResponse()
    {
        var client = CreateClient(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new
                        {
                            authorized = true,
                            authorization_code = "0bb07405-6d44-4b50-a14f-7ae0beff13ad",
                        }
                    ),
                }
            )
        );

        var result = await client.ProcessPaymentAsync(AnyRequest, CancellationToken.None);

        Assert.True(result.Authorized);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", result.AuthorizationCode);
    }

    [Fact]
    public async Task Ok_Declined_ReturnsParsedResponse()
    {
        var client = CreateClient(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new { authorized = false, authorization_code = "" }
                    ),
                }
            )
        );

        var result = await client.ProcessPaymentAsync(AnyRequest, CancellationToken.None);

        Assert.False(result.Authorized);
    }

    [Fact]
    public async Task SendsSnakeCaseBodyToPaymentsEndpoint()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var client = CreateClient(async req =>
        {
            captured = req;
            body = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { authorized = true, authorization_code = "x" }),
            };
        });

        await client.ProcessPaymentAsync(AnyRequest, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("/payments", captured.RequestUri!.AbsolutePath);
        Assert.Contains("\"card_number\"", body);
        Assert.Contains("\"expiry_date\":\"12/2027\"", body);
        Assert.Contains("\"cvv\":\"123\"", body);
    }

    [Fact]
    public async Task ServiceUnavailable_ThrowsBankUnavailable()
    {
        var client = CreateClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        );

        await Assert.ThrowsAsync<BankUnavailableException>(() =>
            client.ProcessPaymentAsync(AnyRequest, CancellationToken.None)
        );
    }

    [Fact]
    public async Task BadRequest_ThrowsBankRequestMalformed()
    {
        var client = CreateClient(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(
                        new { error_message = "Not all required properties were sent" }
                    ),
                }
            )
        );

        await Assert.ThrowsAsync<BankRequestMalformedException>(() =>
            client.ProcessPaymentAsync(AnyRequest, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Timeout_ThrowsBankUnavailable()
    {
        var client = CreateClient(async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2)); // longer than the 200 ms client timeout
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAsync<BankUnavailableException>(() =>
            client.ProcessPaymentAsync(AnyRequest, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ConnectionFailure_ThrowsBankUnavailable()
    {
        var client = CreateClient(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BankUnavailableException>(() =>
            client.ProcessPaymentAsync(AnyRequest, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CallerCancellation_IsNotReportedAsBankFailure()
    {
        using var cts = new CancellationTokenSource();
        var client = CreateClient(async _ =>
        {
            cts.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // The caller gave up, so the original cancellation should surface, not a bank-unavailable error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ProcessPaymentAsync(AnyRequest, cts.Token)
        );
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => respond(request);
    }
}
