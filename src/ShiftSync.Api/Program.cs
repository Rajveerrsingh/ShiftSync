using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShiftSync.Api.Auth;
using ShiftSync.Application;
using ShiftSync.Application.Abstractions;
using ShiftSync.Infrastructure;
using ShiftSync.Infrastructure.Persistence;
using ShiftSync.Infrastructure.Seed;

const string DevJwtKey = "dev-only-shiftsync-key-change-for-real-use";

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "JWT signing key is missing. Set Jwt:Key or the Jwt__Key environment variable.");
    }

    jwtKey = DevJwtKey;
    builder.Configuration["Jwt:Key"] = jwtKey;
}

if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShiftSync API",
        Version = "v1",
        Description = "Weekly staff scheduling for a retail store. Managers build the grid. Associates request swaps and time off."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT from POST /api/auth/login.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var issuer = builder.Configuration["Jwt:Issuer"] ?? "ShiftSync";
var audience = builder.Configuration["Jwt:Audience"] ?? "ShiftSync";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return Results.Problem(
                    title: "Unauthorized",
                    detail: "Sign in again.",
                    statusCode: StatusCodes.Status401Unauthorized).ExecuteAsync(context.HttpContext);
            },
            OnForbidden = context => Results.Problem(
                title: "Not allowed",
                detail: "A manager has to do that.",
                statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context.HttpContext)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShiftSync");
    options.DocumentTitle = "ShiftSync API";
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (app.Environment.IsDevelopment())
            context.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var database = scope.ServiceProvider.GetRequiredService<DatabaseOptions>();
    if (database.Provider == "Sqlite")
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    if (!app.Environment.IsEnvironment("Testing"))
        await DemoSeeder.SeedAsync(db);

    app.Logger.LogInformation("ShiftSync is using the {Provider} database.", database.Provider);
}

app.Run();

public partial class Program;
