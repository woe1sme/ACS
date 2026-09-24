using System.Data.Common;
using BuildingBlocks.ServiceDefaults.Errors;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.ServiceDefaults.Grpc;

/// <summary>Maps application exceptions to gRPC status codes without leaking internal details.</summary>
public sealed class ErrorMappingInterceptor(ILogger<ErrorMappingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var status = ex switch
            {
                ValidationException => new Status(StatusCode.InvalidArgument, ex.Message),
                NotFoundException => new Status(StatusCode.NotFound, ex.Message),
                ConflictException => new Status(StatusCode.FailedPrecondition, ex.Message),
                DependencyUnavailableException or DbException or TimeoutException => new Status(StatusCode.Unavailable, "A dependency is temporarily unavailable."),
                OperationCanceledException => new Status(StatusCode.Cancelled, "The call was cancelled."),
                _ => new Status(StatusCode.Internal, "Internal error."),
            };

            if (status.StatusCode is StatusCode.Internal or StatusCode.Unavailable)
            {
                logger.LogError(ex, "gRPC call {Method} failed with {StatusCode}", context.Method, status.StatusCode);
            }

            throw new RpcException(status);
        }
    }
}
