using InfraAis.Options;
using InfraAis.Services;
using InfraAis.Repositories;
using Microsoft.AspNetCore.Identity;
using InfraAis.Authentication;
using InfraAis.Models.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Threading.RateLimiting;
using InfraAis.Middleware;
using InfraAis.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using InfraAis.Validation;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOptions<AisStreamOptions>().Bind(builder.Configuration.GetSection(AisStreamOptions.SectionName));
builder.Services.AddOptions<IngestionOptions>().Bind(builder.Configuration.GetSection(IngestionOptions.SectionName));
builder.Services.AddOptions<AnalyticsOptions>().Bind(builder.Configuration.GetSection(AnalyticsOptions.SectionName));
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt"));
builder.Services.AddHostedService<AisIngestionService>();
builder.Services.AddOptions<AisDbOptions>().Bind(builder.Configuration.GetSection(AisDbOptions.SectionName));
builder.Services.AddScoped<IAisRepository, AisRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IApiUsageRepository, ApiUsageRepository>();
builder.Services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<IVesselAnalyticsService, VesselAnalyticsService>();
builder.Services.AddScoped<IApiClientRepository,ApiClientRepository>();
builder.Services.AddScoped<IvesselIdentifierResolver, VesselIdentifierResolver>();
builder.Services.AddScoped<IPositionQueryValidator, PositionQueryValidator>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()?? throw new InvalidOperationException("JWT configuration is missing");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience) ||jwtSettings.ExpirationMinutes <= 0)
{
    throw new InvalidOperationException("Invalid JWT configuration");
}

byte[] jwtKey = Convert.FromBase64String(jwtSettings.SigningKeyBase64);

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT signing key must contain at least 32 bytes");
}

builder.Services
    .AddAuthentication(options => 
    { 
        options.DefaultScheme = AuthSchemes.ApiKeyOrJwt; 
    options.DefaultChallengeScheme = AuthSchemes.ApiKeyOrJwt;
    })
    .AddScheme<AuthenticationSchemeOptions,CustomAuthenticationHandler>( AuthSchemes.ApiKey, null )
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),

            ValidAlgorithms = new[]
            {
                SecurityAlgorithms.HmacSha256
            },

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            RoleClaimType = "role",
            NameClaimType = JwtRegisteredClaimNames.UniqueName
        };
    })
    .AddPolicyScheme(AuthSchemes.ApiKeyOrJwt,null,options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                string apiKey = context.Request.Headers["X-API-Key"].ToString();

                string authorization = context.Request.Headers.Authorization.ToString();

                if (!string.IsNullOrWhiteSpace(apiKey))
                    return AuthSchemes.ApiKey;

                if (authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }

                return AuthSchemes.ApiKey;
            };
        });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AdminOnly, policy =>
    {

        policy.RequireAuthenticatedUser();
        policy.RequireRole(Roles.Admin);
    });

    options.AddPolicy(AuthPolicies.CanRead, policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.RequireRole( Roles.Admin, Roles.Operator,Roles.ReadOnly);
    });

options.AddPolicy(AuthPolicies.AnalyticsAccess, policy =>
{
    policy.RequireAuthenticatedUser();

    policy.RequireAssertion(context => 
    context.User.IsInRole(Roles.Admin) || context.User.HasClaim( InfraClaimTypes.AnalyticsEnabled,"true" ));
});

});




builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.ApiKeyPerMinute, context =>
    {
        if (context.User.Identity?.AuthenticationType != AuthSchemes.ApiKey)
            return RateLimitPartition.GetNoLimiter("non-api-key");

        Guid keyId = context.User.GetKeyId() ?? throw new InvalidOperationException("Missing key ID claim");
        int limit = context.User.GetRateLimit() ?? throw new InvalidOperationException("Missing rate limit claim");

        if (limit <= 0)
            throw new InvalidOperationException("Invalid rate limit claim");

        return RateLimitPartition.GetFixedWindowLimiter($"{keyId}:{limit}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicies.Login, context =>
    {
        string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsync("Rate limit exceeded", cancellationToken);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    string username = app.Configuration["SeedAdmin:Username"] ?? throw new InvalidOperationException("SeedAdmin:Username is missing");

    string password = app.Configuration["SeedAdmin:Password"] ?? throw new InvalidOperationException("SeedAdmin:Password is missing");

    await AdminUserSeeder.SeedAsync(repo, hasher,username, password);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<MonthlyQuotaMiddleware>();

app.MapControllers();

app.Run();
