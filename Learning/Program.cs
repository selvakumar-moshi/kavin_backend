using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using LearningBackendAPI.Config;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Middleware;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;
using LearningBackendAPI.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using System.Text;

// Load environment variables (must happen before CreateBuilder so AddEnvironmentVariables() picks them up)
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Services
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// Configure Pipeline
ConfigurePipeline(app);

app.Run();

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Add controllers
    services.AddControllers();
    services.AddEndpointsApiExplorer();

    // ========== CORS Configuration (From Your Existing Project) ==========
    var allowedOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    });
    // ========== End CORS Configuration ==========

    // Configure Swagger with JWT Support
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Kavin Backend API",
            Version = "v1",
            Description = "API for Course Management System"
        });

        // Add JWT Authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer' followed by space and your JWT token.\n\nExample: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'"
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

    // Configure MongoDB
    var mongoSettings = configuration.GetSection("MongoDB").Get<DatabaseSettings>();
    var mongoClient = new MongoClient(mongoSettings.ConnectionString);
    var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);
    services.AddSingleton(database);

    // Unique-but-sparse: enforces uniqueness only for documents that actually have an
    // applicationNo (existing/admin users without one are unaffected).
    var users = database.GetCollection<User>(Constants.CollectionNames.Users);
    users.Indexes.CreateOne(new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Ascending(u => u.ApplicationNo),
        new CreateIndexOptions { Unique = true, Sparse = true }));

    // Register Repositories
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<ICourseRepository, CourseRepository>();
    services.AddScoped<IStudyMaterialRepository, StudyMaterialRepository>();
    services.AddScoped<IVideoMaterialRepository, VideoMaterialRepository>();
    services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
    services.AddScoped<IQuizRepository, QuizRepository>();
    services.AddScoped<IQuizAttemptRepository, QuizAttemptRepository>();
    services.AddScoped<IBatchRepository, BatchRepository>();
    services.AddScoped<ICounterRepository, CounterRepository>();
    services.AddScoped<INotificationRepository, NotificationRepository>();

    // Configure AWS S3 (credentials loaded from .env via DotNetEnv)
    var awsAccessKey = configuration["AWS_ACCESS_KEY_ID"];
    var awsSecretKey = configuration["AWS_SECRET_ACCESS_KEY"];
    var awsRegion = configuration["AWS_REGION"];
    var awsBucketName = configuration["AWS_BUCKET_NAME"];

    var s3Settings = new S3Settings { BucketName = awsBucketName ?? "", Region = awsRegion ?? "" };
    services.AddSingleton(Microsoft.Extensions.Options.Options.Create(s3Settings));

    var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
    var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.GetBySystemName(awsRegion));
    services.AddSingleton<IAmazonS3>(s3Client);
    services.AddScoped<IFileStorageService, S3FileStorageService>();

    // Register Services
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<ICourseService, CourseService>();
    services.AddScoped<IStudyMaterialService, StudyMaterialService>();
    services.AddScoped<IVideoMaterialService, VideoMaterialService>();
    services.AddScoped<IEnrollmentService, EnrollmentService>();
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IQuizService, QuizService>();
    services.AddScoped<IExcelExportService, ExcelExportService>();
    services.AddScoped<IDashboardService, DashboardService>();
    services.AddScoped<IBatchService, BatchService>();
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IFreeMaterialService, FreeMaterialService>();
    services.AddScoped<IJwtService, JwtService>();

    // Register Validators (Only Register Validator, CourseValidator removed)
    services.AddScoped<RegisterValidator>();
    // services.AddScoped<CourseValidator>(); // ❌ REMOVED

    // Register Response Helper
    services.AddScoped<ResponseHelper>();

    // Configure JWT Authentication
    var jwtSettings = configuration.GetSection("Jwt");
    var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]);

    services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(x =>
    {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    services.AddAuthorization();
}

void ConfigurePipeline(WebApplication app)
{
    // Global Error Handling Middleware
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // ========== Use CORS (From Your Existing Project) ==========
    app.UseCors("AllowAll");
    // ========== End CORS ==========

    // Swagger
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kavin Backend API v1");
        });
    }

    app.UseHttpsRedirection();

    // JWT Middleware
    app.UseMiddleware<JwtMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Seed admin user
    SeedAdminUser(app.Services);

    // Backfill applicationNo for any users created before this field existed
    BackfillApplicationNumbers(app.Services);
}

void SeedAdminUser(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var adminEmail = configuration["Admin:Email"];
    var adminPassword = configuration["Admin:Password"];

    if (adminEmail != null && adminPassword != null)
    {
        var existingAdmin = userRepository.GetByEmailAsync(adminEmail).GetAwaiter().GetResult();
        if (existingAdmin == null)
        {
            var admin = new User
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                FirstName = "Admin",
                LastName = "User",
                PhoneNumber = "0000000000",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            userRepository.CreateAsync(admin).GetAwaiter().GetResult();
        }
    }
}

void BackfillApplicationNumbers(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var counterRepository = scope.ServiceProvider.GetRequiredService<ICounterRepository>();

    var usersMissingApplicationNo = userRepository.GetUsersWithoutApplicationNoAsync().GetAwaiter().GetResult();
    if (usersMissingApplicationNo.Count == 0)
    {
        return;
    }

    // Admins first (so the original admin lands on the first number, e.g. RSK-1000), then
    // everyone else in the order they originally registered.
    var orderedUsers = usersMissingApplicationNo
        .OrderBy(u => u.Role == Constants.Roles.Admin ? 0 : 1)
        .ThenBy(u => u.CreatedAt)
        .ToList();

    foreach (var user in orderedUsers)
    {
        var sequence = counterRepository.GetNextSequenceAsync(Constants.ApplicationNumber.CounterName).GetAwaiter().GetResult();
        user.ApplicationNo = $"{Constants.ApplicationNumber.Prefix}{Constants.ApplicationNumber.Offset + sequence}";
        userRepository.UpdateAsync(user.Id, user).GetAwaiter().GetResult();
    }
}