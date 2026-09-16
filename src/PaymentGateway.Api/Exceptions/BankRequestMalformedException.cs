namespace PaymentGateway.Api.Exceptions;

public sealed class BankRequestMalformedException(string message) : Exception(message) { }

