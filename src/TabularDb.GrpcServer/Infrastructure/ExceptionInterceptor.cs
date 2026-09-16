using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using TabularDb.Core.Exceptions;
using TabularDb.Storage;

namespace TabularDb.GrpcServer.Infrastructure;

public sealed class ExceptionInterceptor(ILogger<ExceptionInterceptor> logger) : Interceptor
{
    public const string ErrorsTrailer = "validation-errors-bin";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (ValidationException ex)
        {
            var trailers = new Metadata();
            foreach (var error in ex.Errors)
                trailers.Add(ErrorsTrailer, Encoding.UTF8.GetBytes(error));
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Помилка валідації"), trailers);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (AlreadyExistsException ex)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
        }
        catch (StorageException ex)
        {
            logger.LogWarning(ex, "Storage error in {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error in {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "Внутрішня помилка сервера"));
        }
    }
}
