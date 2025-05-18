using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Polly;
using Polly.Extensions.Http;
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
            .WithOrigins(
                "http://localhost:3000", 
                "https://localhost:3000",
                "http://localhost:3001", 
                "https://localhost:3001"
            ) // Thay đổi theo domain của frontend
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
    // Increase timeout for better reliability with slow external APIs
    client.Timeout = TimeSpan.FromSeconds(10);
    
    // Add default headers if needed
    client.DefaultRequestHeaders.Add("User-Agent", "SocialApp-Backend");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.SetHandlerLifetime(TimeSpan.FromMinutes(5))  // Set the lifetime of the HttpClientHandler
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Configure proxy settings
    UseProxy = false,
    
    // Configure TLS/SSL (only for development)
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    
    // Add additional performance settings
    MaxConnectionsPerServer = 100,
    UseCookies = false
})
// Add retry policy to handle transient failures
.AddTransientHttpErrorPolicy(policy => policy
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt))))
// Add circuit breaker to prevent cascading failures
.AddTransientHttpErrorPolicy(policy => policy
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

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


