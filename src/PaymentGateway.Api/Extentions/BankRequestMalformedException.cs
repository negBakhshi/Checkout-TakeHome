namespace PaymentGateway.Api.Extentions;

public sealed class BankRequestMalformedException(string message) : Exception(message) { }

