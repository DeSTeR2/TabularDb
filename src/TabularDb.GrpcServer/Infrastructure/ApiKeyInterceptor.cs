using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

namespace TabularDb.GrpcServer.Infrastructure;

public sealed class ApiKeyInterceptor(IOptions<SecurityOptions> options) : Interceptor
{
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var expected = options.Value.ApiKey;
        if (!string.IsNullOrEmpty(expected))
        {
            var actual = context.RequestHeaders.GetValue(SecurityOptions.HeaderName) ?? "";
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected)))
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Невірний або відсутній API-ключ"));
        }
        return continuation(request, context);
    }
}
