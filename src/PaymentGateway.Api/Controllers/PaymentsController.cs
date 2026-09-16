using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(
    IPaymentService paymentService,
    IPaymentsRepository paymentsRepository,
    IValidator<PostPaymentRequest> validator
) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = paymentsRepository.Get(id);

        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RejectedPaymentResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken
    )
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return BadRequest(
                new RejectedPaymentResponse
                {
                    Status = PaymentStatus.Rejected,
                    Errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
                }
            );
        }

        try
        {
            var response = await paymentService.PostPaymentAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (BankUnavailableException ex)
        {
            // The bank never gave us a decision, so this is neither Authorized nor Declined.
            // Mirror the bank's 503 so the merchant knows to retry.
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Acquiring bank unavailable",
                detail: ex.Message
            );
        }
    }
}

