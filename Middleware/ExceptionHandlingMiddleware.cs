using Npgsql;

namespace YantaBackEnd.Middleware;

public sealed class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public static AppException NotFound(string message) => new(StatusCodes.Status404NotFound, message);
    public static AppException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static AppException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
}

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "ข้อมูลนี้มีอยู่แล้ว");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            await WriteAsync(
                context,
                StatusCodes.Status409Conflict,
                "ข้อมูลนี้ถูกใช้งานอยู่หรืออ้างอิงข้อมูลที่ไม่มีอยู่ จึงดำเนินการไม่ได้");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteAsync(context, StatusCodes.Status500InternalServerError, "เกิดข้อผิดพลาดภายในระบบ");
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        // ไม่เรียก Response.Clear() เพราะจะลบ header CORS ที่ middleware ก่อนหน้าใส่ไว้
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = message
        });
    }
}
