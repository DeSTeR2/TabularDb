using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TabularDb.Core.Exceptions;
using TabularDb.Storage;

namespace TabularDb.WebApi.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public const string ErrorsKey = "errors";

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException ex => new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [ErrorsKey] = ex.Errors.ToArray(),
            })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Помилка валідації",
            },
            NotFoundException ex => Problem(StatusCodes.Status404NotFound, ex.Message),
            AlreadyExistsException ex => Problem(StatusCodes.Status409Conflict, ex.Message),
            StorageException { IsIoError: true } => Problem(StatusCodes.Status500InternalServerError, "Помилка доступу до сховища на сервері"),
            StorageException ex => Problem(StatusCodes.Status422UnprocessableEntity, ex.Message),
            BadHttpRequestException ex => Problem(ex.StatusCode, ex.Message),
            _ => Problem(StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера"),
        };

        if (problem.Status >= 500)
            logger.LogError(exception, "Unhandled error for {Path}", context.Request.Path);
        else if (exception is StorageException or BadHttpRequestException)
            logger.LogWarning(exception, "Storage error for {Path}", context.Request.Path);

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title) => new() { Status = status, Title = title };
}
