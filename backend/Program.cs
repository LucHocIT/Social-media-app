using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.SqlServer.Diagnostics.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Polly;
using Polly.Extensions.Http;
using SocialApp.Models;
using SocialApp.Services.Auth;
using SocialApp.Services.Email;
using SocialApp.Services.User;
using SocialApp.Services.Utils;
using SocialApp.Services.Post;
using SocialApp.Services.Comment;
using SocialApp.Services.Chat;
using SocialApp.Hubs;
using SocialApp.Filters;
using Microsoft.OpenApi.Models;
using Npgsql;

// Load .env file if it exists (only in development)
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    DotEnv.Load(envFile);
}

var builder = WebApplication.CreateBuilder(args);

// Add configuration from environment variables
builder.Configuration.AddEnvironmentVariables();

// Debug: Log environment check
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"DATABASE_URL exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"))}");

// Function to convert PostgreSQL URL to connection string
string ConvertPostgresUrlToConnectionString(string databaseUrl)
{
    try
    {
        var uri = new Uri(databaseUrl);
        var host = uri.Host;
        var port = uri.Port;
        var database = uri.AbsolutePath.TrimStart('/');
        
        // Handle userInfo more carefully for special characters
        var userInfo = uri.UserInfo.Split(':');
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        
        Console.WriteLine($"Parsed connection - Host: {host}, Port: {port}, Database: {database}, Username: {username}");
        Console.WriteLine($"Password length: {password.Length} characters");
        
        return connectionString;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
        throw;
    }
}

// Configure NpgsqlDataSource for PostgreSQL connections
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Try to get connection string from environment variable if not found in config
if (string.IsNullOrEmpty(connectionString))
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        Console.WriteLine($"Raw DATABASE_URL: {databaseUrl.Substring(0, Math.Min(50, databaseUrl.Length))}...");
        
        if (databaseUrl.StartsWith("postgresql://") || databaseUrl.StartsWith("postgres://"))
        {
            connectionString = ConvertPostgresUrlToConnectionString(databaseUrl);
            Console.WriteLine("Converted DATABASE_URL to connection string format");
            Console.WriteLine($"Final connection string: {connectionString.Substring(0, Math.Min(100, connectionString.Length))}...");
        }
        else
        {
            connectionString = databaseUrl;
            Console.WriteLine("Using DATABASE_URL as-is");
        }
    }
    else
    {
        Console.WriteLine("WARNING: DATABASE_URL environment variable is empty or not set!");
    }
}

// If still empty, try to build from individual env vars (for development)
if (string.IsNullOrEmpty(connectionString))
{
    var host = Environment.GetEnvironmentVariable("DB_HOST");
    var database = Environment.GetEnvironmentVariable("DB_DATABASE");
    var username = Environment.GetEnvironmentVariable("DB_USER");
    var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
    
    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(database))
    {
        connectionString = $"Host={host};Database={database};Username={username};Password={password};";
        Console.WriteLine("Built connection string from individual env vars");
    }
}

if (!string.IsNullOrEmpty(connectionString) && (connectionString.Contains("postgres") || connectionString.Contains("postgresql")))
{
    Console.WriteLine("Using PostgreSQL connection");
    // Register NpgsqlDataSource for better connection pooling
    try
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        builder.Services.AddSingleton(dataSourceBuilder.Build());
        Console.WriteLine("NpgsqlDataSource configured successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error configuring NpgsqlDataSource: {ex.Message}");
        throw new InvalidOperationException($"Invalid PostgreSQL connection string format: {ex.Message}");
    }
}
else if (!string.IsNullOrEmpty(connectionString))
{
    // For SQL Server connections in development
    Console.WriteLine("Using SQL Server connection for development");
}
else
{
    Console.WriteLine("ERROR: No database connection string found!");
    throw new InvalidOperationException("Database connection string is required. Please set DATABASE_URL environment variable or configure DefaultConnection in appsettings.");
}

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure DateTime serialization
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Social App API",
        Version = "v1",
        Description = "API for SocialApp"
    });

    // JWT authentication for Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme. Example: \"Authorization: Bearer {token}\"",
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
        }    });    // Configure Swagger to handle form file uploads properly
    c.CustomSchemaIds(type => type.FullName);
    
    // Add support for file uploads in Swagger
    c.OperationFilter<FileUploadOperationFilter>();
});

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder
            .WithOrigins(
                "http://localhost:3000", 
                "https://localhost:3000",
                "http://frontend:80",
                "http://host.docker.internal:3000",
                "https://socailapp-j7s9.onrender.com",
                "https://socialapp-backend.onrender.com",
                "https://socialapp-frontend.onrender.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()); // Important for SignalR
});

// DbContext
builder.Services.AddDbContext<SocialMediaDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // Try environment variable if config is empty and convert if needed
    if (string.IsNullOrEmpty(connectionString))
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            if (databaseUrl.StartsWith("postgresql://") || databaseUrl.StartsWith("postgres://"))
            {
                connectionString = ConvertPostgresUrlToConnectionString(databaseUrl);
            }
            else
            {
                connectionString = databaseUrl;
            }
        }
    }
    
    if (!string.IsNullOrEmpty(connectionString) && (connectionString.Contains("postgres") || connectionString.Contains("postgresql")))
    {
        // Use PostgreSQL for production (Render)
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null
            );
        });
    }
    else if (!string.IsNullOrEmpty(connectionString))
    {
        // Use SQL Server for development
        options.UseSqlServer(connectionString);
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
            warnings.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS);
        });
    }
    else
    {
        throw new InvalidOperationException("Database connection string is required. Please set DATABASE_URL environment variable or DefaultConnection in appsettings.");
    }
    
    // Add logging for debugging in development only
    if (builder.Environment.IsDevelopment())
    {
        options.LogTo(Console.WriteLine, LogLevel.Information);
        options.EnableSensitiveDataLogging(false);
        options.EnableDetailedErrors(true);
    }
});

// Register services
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<ISocialAuthService, SocialAuthService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IUserBlockService, UserBlockService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Post services - separated for better maintainability
builder.Services.AddScoped<IPostManagementService, PostManagementService>();
builder.Services.AddScoped<IPostQueryService, PostQueryService>();
builder.Services.AddScoped<IPostMediaService, PostMediaService>();

builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ICommentReportService, CommentReportService>();

// Chat services
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ISimpleChatService, SimpleChatService>(); // Facade that combines both services
builder.Services.AddScoped<IMessageReactionService, MessageReactionService>();

// Notification services
builder.Services.AddScoped<SocialApp.Services.Notification.INotificationService, SocialApp.Services.Notification.NotificationService>();

// User presence service - can be disabled via configuration
var enableUserPresence = builder.Configuration.GetValue("EnableUserPresence", true);
if (enableUserPresence)
{
    builder.Services.AddHostedService<UserPresenceService>();
}
else
{
    var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Program");
    logger.LogInformation("UserPresenceService is disabled via configuration");
}

// SignalR
builder.Services.AddSignalR();

// Message services

// HttpClient for email verification
builder.Services.AddHttpClient("EmailVerificationClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddTransientHttpErrorPolicy(policy => policy
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt))));

// HttpClient for social authentication
builder.Services.AddHttpClient<ISocialAuthService, SocialAuthService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
.AddTransientHttpErrorPolicy(policy => policy
    .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// HttpClient specifically for Cloudinary with enhanced SSL/TLS settings
builder.Services.AddHttpClient("CloudinaryClient", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Longer timeout for file uploads
    client.DefaultRequestHeaders.Add("User-Agent", "SocialApp/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    
    // Enhanced SSL/TLS settings specifically for Cloudinary
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
    handler.MaxConnectionsPerServer = 10; // Limit concurrent connections
    handler.UseProxy = false; // Disable proxy to avoid connection issues
    
    // More specific certificate validation for Cloudinary
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        // Allow Cloudinary certificates specifically
        if (message.RequestUri?.Host.Contains("cloudinary.com") == true ||
            message.RequestUri?.Host.Contains("res.cloudinary.com") == true)
        {
            return true;
        }
        
        // For other hosts, use default validation
        return errors == System.Net.Security.SslPolicyErrors.None;
    };
    
    return handler;
})
.AddTransientHttpErrorPolicy(policy => policy
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromMilliseconds(1000 * Math.Pow(2, retryAttempt)))); // Exponential backoff

// Configure HttpClient with SSL/TLS settings for better connectivity
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });
    
    http.ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        
        // Configure SSL/TLS settings for better compatibility
        handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            // For production, you should implement proper certificate validation
            // For now, we'll allow all certificates to resolve SSL issues
            return true;
        };
        
        return handler;
    });
    
    // Add retry policy for transient failures
    http.AddTransientHttpErrorPolicy(policy => policy
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
            builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key not configured")))
    };
    
    // Configure JWT for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Configure the HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Social App API v1"));
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SimpleChatHub>("/chatHub");

// Seed the database
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
    try
    {
        // Test connection first
        if (!context.Database.CanConnect())
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Cannot connect to database. Skipping seeding.");
            return;
        }

        // Check if there are pending migrations
        var pendingMigrations = context.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Applying {0} pending migrations: {1}",
                pendingMigrations.Count,
                string.Join(", ", pendingMigrations));

            // Apply pending migrations
            context.Database.Migrate();
        }
        
        // Create admin user if it doesn't exist
        if (!context.Users.Any(u => u.Username == "admin"))
        {
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FirstName = "Admin",
                LastName = "User",
                Role = "Admin",
                CreatedAt = DateTime.Now,
                LastActive = DateTime.Now
            };

            context.Users.Add(adminUser);
            context.SaveChanges();

            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Admin user created successfully");
        }
    }
    catch (Exception ex)
    {
        // Don't crash the app if seeding fails
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database seeding failed. This is not critical for API operation.");
    }
}


