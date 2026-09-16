namespace PaymentGateway.Api.Exceptions;

public sealed class BankUnavailableException(string message) : Exception(message) { }

