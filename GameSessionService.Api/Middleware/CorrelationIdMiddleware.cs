namespace GameSessionService.Api.Middleware;

/// <summary>
/// Middleware to handle correlation ID for request tracking
/// Extracts correlation ID from header or generates a new one
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from request header
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        // If not present, generate a new one
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Store in HttpContext.Items for use throughout the request pipeline
        context.Items["CorrelationId"] = correlationId;

        // Add to response header for client tracking
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the correlation ID middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}

