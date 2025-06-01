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

// Load .env file if it exists (this should be before creating the builder)
DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

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
            .WithOrigins("http://localhost:3000", "https://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()); // Important for SignalR
});

// DbContext
builder.Services.AddDbContext<SocialMediaDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
        warnings.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS);
    });
});

// Register services
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<ISocialAuthService, SocialAuthService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IUserBlockService, UserBlockService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ICommentReportService, CommentReportService>();

// Chat services
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ISimpleChatService, SimpleChatService>(); // Facade that combines both services
builder.Services.AddScoped<IMessageReactionService, MessageReactionService>();

// User presence service
builder.Services.AddHostedService<UserPresenceService>();

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
    }
    catch (Exception ex)
    {
        // Migrations might already be applied
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "An error occurred during migration. Database might already be up to date.");
    }

    // Create admin user if it doesn't exist
    if (!context.Users.Any(u => u.Username == "admin"))
    {
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@example.com",            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
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


