using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Polly;
using Polly.Extensions.Http;
using SocialApp.Models;
using SocialApp.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Thêm cấu hình Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Social App API", 
        Version = "v1",
        Description = "API cho ứng dụng mạng xã hội SocialApp",
        Contact = new OpenApiContact
        {
            Name = "Social App Team",
            Email = "contact@socialapp.example.com"
        }
    });
    
    // Cấu hình xác thực JWT cho Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Social App API v1");
        c.RoutePrefix = "swagger";
    });
}

// Add CORS middleware
app.UseCors("AllowFrontend");

// Add Authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


