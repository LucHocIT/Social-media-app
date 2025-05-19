using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Polly;
using Polly.Extensions.Http;
using SocialApp.Models;
using SocialApp.Services.Auth;
using SocialApp.Services.Email;
using SocialApp.Services.User;
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

// Đăng ký các dịch vụ authentication
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IEmailService, EmailService>();
// AuthService và EmailVerificationCodeService đã được loại bỏ vì không cần thiết

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

// Configure authorization to use role-based permissions
app.UseAuthorization();

app.MapControllers();

// Seed the database with an initial admin user
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SocialMediaDbContext>();
        SeedDatabase(dbContext, app.Configuration);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();

// Database seeding method
void SeedDatabase(SocialMediaDbContext context, IConfiguration configuration)
{
    // Check if database exists and is valid before running migrations
    if (!context.Database.CanConnect())
    {
        // Create database if it doesn't exist
        context.Database.EnsureCreated();
    }
    else 
    {
        // Check if __EFMigrationsHistory table exists
        bool migrationsTableExists = false;
        try
        {
            // Try to query the migrations history table
            migrationsTableExists = context.Database.ExecuteSqlRaw("SELECT 1 FROM __EFMigrationsHistory") > 0;
        }
        catch
        {
            migrationsTableExists = false;
        }

        if (!migrationsTableExists)
        {            // The database exists but no migrations history table, so tables were likely created manually
            // Insert migration records to prevent EF from trying to create existing tables
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = @"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [__EFMigrationsHistory] (
                            [MigrationId] nvarchar(150) NOT NULL,
                            [ProductVersion] nvarchar(32) NOT NULL,
                            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                        )
                    END";
                  if (command.Connection != null && command.Connection.State != System.Data.ConnectionState.Open)
                    command.Connection.Open();
                
                command.ExecuteNonQuery();
                
                // Add all migrations as "already applied"
                command.CommandText = @"
                    IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250519075357_AddRoleAndSoftDelete')
                    BEGIN
                        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) 
                        VALUES ('20250519075357_AddRoleAndSoftDelete', '9.0.0')
                    END";
                command.ExecuteNonQuery();
            }
        }
        else
        {
            // Try to apply any pending migrations safely
            try 
            {
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(ex, "An error occurred during migration. Database might already be up to date.");
                // Continue execution - tables might already exist
            }
        }
    }

    // Check if admin user exists
    if (!context.Users.Any(u => u.Username == "admin"))
    {
        // Create admin user
        var adminUser = new SocialApp.Models.User
        {
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), // Set a strong default password
            FirstName = "Admin",
            LastName = "User",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow
        };

        context.Users.Add(adminUser);
        context.SaveChanges();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Admin user created successfully");
    }
}


