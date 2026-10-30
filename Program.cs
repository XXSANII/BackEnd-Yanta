using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using YantaBackEnd.Authorization;
using YantaBackEnd.Data;
using YantaBackEnd.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

// 2. Authentication: ตรวจสอบ JWT จาก Supabase
//    - Asymmetric (ES256/RS256): ดึง public key จาก JWKS ผ่าน Authority อัตโนมัติ
//    - Legacy HS256: ใช้ Supabase:JwtSecret ถ้ามีการตั้งค่าไว้
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<SupabaseOptions>, IWebHostEnvironment>(
        (options, supabaseOptions, env) =>
        {
            var supabase = supabaseOptions.Value;

            options.Authority = supabase.Issuer;
            options.RequireHttpsMetadata = !env.IsDevelopment();

            // อ่าน claim ชื่อเดิม เช่น "sub" ไม่ถูกแปลงเป็น ClaimTypes.*
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
                // Legacy JWT secret (HS256) — จำกัด algorithm ให้ตรงกัน
                parameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(supabase.JwtSecret));
                parameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            }
            else
            {
                // Signing keys แบบใหม่ — ระบุ algorithm ที่ยอมรับ
                parameters.ValidAlgorithms =
                [
                    SecurityAlgorithms.EcdsaSha256,
                    SecurityAlgorithms.RsaSha256
                ];
            }

            options.TokenValidationParameters = parameters;
        });

// 3. Authorization: Policy "Permission:xxx" ถูกสร้างแบบ dynamic
//    ใช้ [HasPermission("menu.view")] ที่ Controller ได้เลย
builder.Services.AddSingleton<
    IAuthorizationPolicyProvider,
    PermissionPolicyProvider>();

builder.Services.AddAuthorization();

// 4. Handler สำหรับตรวจสอบ Permission ใน PostgreSQL
builder.Services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();

var app = builder.Build();

// Health check: ตรวจสอบการเชื่อมต่อ Database
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

// ต้องเรียก Authentication ก่อน Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();