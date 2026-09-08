using AccountManager.Application.Common;
using AccountManager.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace AccountManager.Api.Mapping;

public static class ApplicationResultMapper
{
    public static IActionResult ToActionResult<T>(ApplicationResult<T> result)
    {
        if (result.Succeeded)
        {
            return new OkObjectResult(result.Value);
        }

        return new ObjectResult(new
        {
            error = result.ErrorCode,
            message = result.ErrorMessage,
            data = result.Value
        })
        {
            StatusCode = MapStatusCode(result.ErrorCode)
        };
    }

    public static IActionResult ToMutationActionResult(
        ApplicationResult<Application.Commands.MoneyMutationResult> result)
    {
        if (result.Succeeded && result.Value is not null)
        {
            var status = result.Value.IdempotentReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
            return new ObjectResult(result.Value) { StatusCode = status };
        }

        return new ObjectResult(new
        {
            error = result.ErrorCode,
            message = result.ErrorMessage,
            data = result.Value
        })
        {
            StatusCode = MapStatusCode(result.ErrorCode)
        };
    }

    private static int MapStatusCode(string? errorCode) => errorCode switch
    {
        DomainErrorCodes.InvalidAmount => StatusCodes.Status400BadRequest,
        ApplicationErrorCodes.IdempotencyKeyRequired => StatusCodes.Status400BadRequest,
        ApplicationErrorCodes.IdempotencyConflict => StatusCodes.Status409Conflict,
        DomainErrorCodes.InsufficientFunds => StatusCodes.Status422UnprocessableEntity,
        ApplicationErrorCodes.CircuitOpenDbWrite => StatusCodes.Status503ServiceUnavailable,
        ApplicationErrorCodes.CircuitOpen => StatusCodes.Status503ServiceUnavailable,
        ApplicationErrorCodes.DbUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };
}
