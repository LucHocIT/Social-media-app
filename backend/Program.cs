using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SocialApp.Models;
using SocialApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", 
        builder => builder
            .WithOrigins("http://localhost:3000", "https://localhost:3000") // Thay đổi theo domain của frontend
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Add DbContext
builder.Services.AddDbContext<SocialApp.Models.SocialMediaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký dịch vụ Authentication
builder.Services.AddScoped<IAuthService, AuthService>();

// Add HttpClient for external API calls with proper timeout and resilience
builder.Services.AddHttpClient("EmailVerificationClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    // Add any other configuration for the HttpClient here
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5))  // Set the lifetime of the HttpClientHandler
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Configure proxy if needed
    UseProxy = false,
    // Configure TLS/SSL
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

// Đăng ký Email Verification Service
builder.Services.AddTransient<IEmailVerificationService, EmailVerificationService>();

// Cấu hình xác thực JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
            builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key", "JWT Key is not configured")))
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sử dụng CORS trước các middleware khác
app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

// Sử dụng middleware authentication và authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

app.Run();


