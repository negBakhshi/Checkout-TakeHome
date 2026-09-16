namespace PaymentGateway.Api.Models.Requests;

public class PostPaymentRequest
{
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    public string? Currency { get; set; }
    public int? Amount { get; set; }
    public string? Cvv { get; set; }
    public string? CardNumber { get; set; }
}
