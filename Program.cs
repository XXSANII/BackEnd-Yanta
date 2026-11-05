using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using YantaBackEnd.Authorization;
using YantaBackEnd.Data;
using YantaBackEnd.Middleware;
using YantaBackEnd.Options;
using YantaBackEnd.Repositories;
using YantaBackEnd.Services;

// map คอลัมน์ snake_case (order_number) -> property (OrderNumber)
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// frontend (ลูกค้า :5173, หลังร้าน :5174) เรียก API จากเบราว์เซอร์ ต้องอนุญาต origin
// ตอน deploy ตั้ง Cors:AllowedOrigins:0, :1 ... เป็นโดเมนจริง
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddOpenApi();

builder.Services.AddSingleton<DapperContext>();

// 1. อ่านค่า Supabase จาก configuration (appsettings / User Secrets / Env)
builder.Services
    .AddOptions<SupabaseOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseOptions.SectionName))
    .Validate(
        o => Uri.TryCreate(o.Url, UriKind.Absolute, out _),
        "Supabase:Url must be an absolute URL.")
    .ValidateOnStart();

// token ของลูกค้าที่สแกน QR (HMAC) ต้องตั้ง TableSession:Secret ใน User Secrets / Env
builder.Services
    .AddOptions<TableSessionOptions>()
    .Bind(builder.Configuration.GetSection(TableSessionOptions.SectionName))
    .Validate(
        o => o.Secret.Length >= 32,
        "TableSession:Secret must be at least 32 characters.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddScheme<AuthenticationSchemeOptions, TableSessionAuthenticationHandler>(
        TableSessionDefaults.Scheme, null);

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<SupabaseOptions>, IWebHostEnvironment>(
        (options, supabaseOptions, env) =>
        {
            var supabase = supabaseOptions.Value;

            options.Authority = supabase.Issuer;
            options.RequireHttpsMetadata = !env.IsDevelopment();

            options.MapInboundClaims = false;

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = supabase.Issuer,

                ValidateAudience = true,
                ValidAudience = supabase.Audience,

                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                ValidateIssuerSigningKey = true
            };

            if (!string.IsNullOrWhiteSpace(supabase.JwtSecret))
            {
                parameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(supabase.JwtSecret));
                parameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            }
            else
            {
                parameters.ValidAlgorithms =
                [
                    SecurityAlgorithms.EcdsaSha256,
                    SecurityAlgorithms.RsaSha256
                ];
            }

            options.TokenValidationParameters = parameters;
        });

builder.Services.AddSingleton<
    IAuthorizationPolicyProvider,
    PermissionPolicyProvider>();

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();

builder.Services.AddSingleton<TableSessionTokenService>();

builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OptionGroupRepository>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<SessionRepository>();

builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OptionGroupService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<SessionService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health/db", async (
    DapperContext db,
    ILogger<Program> logger) =>
{
    try
    {
        using var connection = db.CreateConnection();

        var result = await connection.QuerySingleAsync<int>("SELECT 1");

        return Results.Ok(new
        {
            status = "healthy",
            database = result == 1 ? "connected" : "unknown"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database health check failed");

        return Results.Problem(
            title: "Database connection failed",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();