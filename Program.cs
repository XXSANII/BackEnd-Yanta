using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using YantaBackEnd.Authorization;
using YantaBackEnd.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<DapperContext>();

const string supabaseIssuer =
    "https://rwpoygtafyxnunazzisl.supabase.co/auth/v1";

// 1. Authentication: ตรวจสอบ JWT จาก Supabase
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = supabaseIssuer;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabaseIssuer,

            ValidateAudience = true,
            ValidAudience = "authenticated",

            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,

            ValidateIssuerSigningKey = true
        };
    });

// 2. รายการ Permission ที่มีใน Database
var permissions = new[]
{
    "menu.manage",
    "menu.view",
    "order.create",
    "order.update_status",
    "order.view",
    "payment.confirm",
    "payment.view",
    "report.view",
    "session.close",
    "session.open",
    "session.view",
    "settings.manage",
    "staff.manage",
    "staff.view",
    "table.manage",
    "table.view"
};

// 3. Authorization: สร้าง Policy แยกตาม Permission
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in permissions)
    {
        options.AddPolicy(
            $"Permission:{permission}",
            policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.AddRequirements(
                    new PermissionRequirement(permission));
            });
    }
});

// 4. ลงทะเบียน Handler สำหรับตรวจสอบ Permission ใน PostgreSQL
builder.Services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();

var app = builder.Build();

// Health check: ตรวจสอบการเชื่อมต่อ Database
app.MapGet("/health/db", async (DapperContext db) =>
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
    catch (Exception)
    {
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