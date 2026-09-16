using System.Net;
using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.BankSimulator;

public class BankClient(HttpClient httpClient) : IBankClient
{
    public async Task<BankResponse> ProcessPaymentAsync(
        BankPaymentRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/payments",
                request,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var bankResponse = await response.Content.ReadFromJsonAsync<BankResponse>(
                    cancellationToken
                );

                return bankResponse
                    ?? throw new BankUnavailableException("Bank returned an empty response.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new BankRequestMalformedException(
                    $"Bank rejected the request as malformed: {errorBody}"
                );
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new BankUnavailableException("Bank is currently unavailable");
            }

            throw new BankUnavailableException(
                $"Bank returned an unexpected status code: {(int)response.StatusCode}."
            );
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BankUnavailableException("The bank did not respond in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new BankUnavailableException($"Could not reach the bank: {ex.Message}");
        }
    }
}

