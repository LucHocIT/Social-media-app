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
using SocialApp.Services.Utils;
using Microsoft.OpenApi.Models;

// Load .env file if it exists (this should be before creating the builder)
DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
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
        }
    });
});

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder
            .WithOrigins("http://localhost:3000", "https://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// DbContext
builder.Services.AddDbContext<SocialMediaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<ISocialAuthService, SocialAuthService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IEmailService, EmailService>();

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
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
            builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key not configured")))
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
        
        // Apply any pending migrations
        context.Database.Migrate();
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
            Email = "admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
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


