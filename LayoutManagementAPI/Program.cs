using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<RealEstateDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("RealEstate")));

builder.Services.AddScoped<UserAccountService>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("The 'Jwt' configuration section is required.");
if (string.IsNullOrWhiteSpace(jwt.Issuer) || jwt.Key.Length < 32 || jwt.AccessTokenHours < 1)
{
    throw new InvalidOperationException(
        "Configuration requires Jwt:Issuer, a Jwt:Key of at least 32 characters, and a positive Jwt:AccessTokenHours.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<LayoutExtractionOptions>(builder.Configuration.GetSection("LayoutExtraction"));
builder.Services.AddHttpClient("LayoutExtraction");
builder.Services.AddScoped<ILayoutExtractionService, LayoutExtractionService>();
builder.Services.AddScoped<LayoutWorkflow>();
builder.Services.AddScoped<LayoutAccessService>();
builder.Services.AddHostedService<RegistrationReminderService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray()
    ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Blazor", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

// No HTTPS redirect on the API: it is called server-to-server over HTTP by the Blazor
// app, and a 307 redirect to HTTPS drops the Authorization header (causing 401s). TLS is
// terminated upstream (e.g. Azure App Service) in hosted environments.

app.UseCors("Blazor");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var accounts = context.RequestServices.GetRequiredService<UserAccountService>();
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await accounts.FindByIdAsync(id);
        if (user is null || user.LoginRestricted)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("This account cannot sign in. Ask an administrator.");
            return;
        }
    }

    await next();
});
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
