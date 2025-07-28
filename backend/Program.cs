using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.SqlServer.Diagnostics.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Collections;
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
Console.WriteLine($"DB_CONNECTION_STRING exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"))}");

// List DB environment variables for debugging
Console.WriteLine("DB environment variables:");
var dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
Console.WriteLine($"  DB_CONNECTION_STRING: {(string.IsNullOrEmpty(dbConnectionString) ? "NOT SET" : "SET")}");
Console.WriteLine($"DB_CONNECTION_STRING exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"))}");

// Debug: List all environment variables starting with DB
Console.WriteLine("Environment variables starting with DB:");
foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
{
    var key = env.Key?.ToString();
    if (key != null && key.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {key}: {(env.Value?.ToString()?.Length > 0 ? "SET" : "EMPTY")}");
    }
}

// Get database connection string - simplified to only use DB_CONNECTION_STRING
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("Using DB_CONNECTION_STRING from environment variable");
    }
    else
    {
        Console.WriteLine("ERROR: DB_CONNECTION_STRING environment variable is not set!");
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
                "https://social-media-app-dmfz.onrender.com",
                "https://socailapp-frontend-hwzc.onrender.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials() // Important for SignalR
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))); // Cache preflight for 10 minutes
    
    // Add a more permissive policy for production debugging (use carefully)
    options.AddPolicy("DevelopmentCors",
        builder => builder
            .SetIsOriginAllowed(origin => true) // Allow all origins in development
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// DbContext
builder.Services.AddDbContext<SocialMediaDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // Use DB_CONNECTION_STRING environment variable  
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
    }
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        // Use PostgreSQL
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null
            );
        });
        
        // Configure PostgreSQL to handle DateTime correctly
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        
        Console.WriteLine("Database configured with PostgreSQL");
    }
    else
    {
        throw new InvalidOperationException("Database connection string is required. Please set DB_CONNECTION_STRING environment variable.");
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

// User presence service - temporarily disabled for debugging
var enableUserPresence = builder.Configuration.GetValue("EnableUserPresence", false); // Changed to false
if (enableUserPresence)
{
    builder.Services.AddHostedService<UserPresenceService>();
}
else
{
    var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Program");
    logger.LogInformation("UserPresenceService is disabled for debugging database connection");
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
// Enable Swagger for both Development and Production (for testing purposes)
app.UseSwagger();
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Social App API v1");
    c.RoutePrefix = "swagger"; // Set Swagger UI at /swagger
});

if (app.Environment.IsDevelopment())
{
    // Use more permissive CORS in development
    app.UseCors("DevelopmentCors");
}
else
{
    // Use strict CORS in production
    app.UseCors("AllowFrontend");
}

// Add CORS headers manually as fallback
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].FirstOrDefault();
    if (!string.IsNullOrEmpty(origin))
    {
        var allowedOrigins = new[]
        {
            "http://localhost:3000",
            "https://localhost:3000", 
            "https://socailapp-frontend-hwzc.onrender.com"
        };
        
        if (allowedOrigins.Contains(origin))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
        }
    }
    
    // Handle preflight requests
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        return;
    }
    
    await next();
});
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

        // Ensure database is created and apply migrations
        try 
        {
            Console.WriteLine("Checking database and applying migrations...");
            
            // Ensure database exists
            context.Database.EnsureCreated();
            
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
            else
            {
                Console.WriteLine("No pending migrations found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database migration failed: {ex.Message}");
            Console.WriteLine("Using EnsureCreated as fallback...");
            
            // Fallback: Just ensure database and tables exist
            context.Database.EnsureCreated();
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


