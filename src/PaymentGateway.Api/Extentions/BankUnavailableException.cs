namespace PaymentGateway.Api.Extentions;

public sealed class BankUnavailableException(string message) : Exception(message) { }

