using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Data;
using Microsoft.AspNetCore.Identity;
using OpsPilot.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Api.Security;
using OpsPilot.Api.Background;
using OpsPilot.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddPolicy("AngularDevelopment", policy =>
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddDbContext<OpsPilotDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OpsPilot")
        ?? throw new InvalidOperationException(
            "ConnectionStrings__OpsPilot is required.")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RabbitMqEventPublisher>();
if (builder.Configuration.GetValue("Messaging:Enabled", true))
{
    builder.Services.AddHostedService<OutboxProcessor>();
    builder.Services.AddHostedService<NotificationWorker>();
}
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<OpsPilotDbContext>()
    .AddSignInManager();
builder.Services.AddScoped<JwtTokenService>();
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt__Key is required.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Jwt__Issuer is required.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Jwt__Audience is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Convert.FromBase64String(jwtKey)),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "ReporterOnly",
        policy => policy.RequireRole("Reporter"));

    options.AddPolicy(
        "ResponderOnly",
        policy => policy.RequireRole("Responder"));

    options.AddPolicy(
        "AdministratorOnly",
        policy => policy.RequireRole("Administrator"));

    options.AddPolicy(
        "ResponderOrAdministrator",
        policy => policy.RequireRole(
            "Responder",
            "Administrator"));
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedRolesAsync(
        scope.ServiceProvider);
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exceptionFeature =
            context.Features.Get<IExceptionHandlerFeature>();

        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        if (exceptionFeature?.Error is not null)
        {
            logger.LogError(
                exceptionFeature.Error,
                "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            })
            .ExecuteAsync(context);
    });
});
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    var logger = context.RequestServices
        .GetRequiredService<ILogger<Program>>();

    logger.LogInformation(
        "HTTP {Method} {Path} started. TraceId: {TraceId}",
        context.Request.Method,
        context.Request.Path,
        context.TraceIdentifier);

    await next();

    logger.LogInformation(
        "HTTP {Method} {Path} completed with {StatusCode}. TraceId: {TraceId}",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        context.TraceIdentifier);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AngularDevelopment");
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () =>
    Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { }