using System.Threading.RateLimiting;
using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Endpoints;
using DenialsCommandCenter.Api.Ingestion;
using DenialsCommandCenter.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KestrelServerOptions = Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions;

var builder = WebApplication.CreateBuilder(args);

// Configuration is read when services resolve (not here), so test hosts can override settings.
builder.Services.AddValidatedOptions<AppOptions>(AppOptions.Section);
builder.Services.AddValidatedOptions<AuthOptions>(AuthOptions.Section);
builder.Services.AddValidatedOptions<GeminiOptions>(GeminiOptions.Section);
builder.Services.AddValidatedOptions<GraphMailOptions>(GraphMailOptions.Section);
builder.Services.AddValidatedOptions<LimitsOptions>(LimitsOptions.Section);

// The largest legitimate body is a short note, so anything bigger is refused before it is buffered.
builder.Services.AddOptions<KestrelServerOptions>()
    .Configure<LimitsOptions>((kestrel, limits) => kestrel.Limits.MaxRequestBodySize = limits.MaxRequestBodyBytes);

builder.Services.AddDbContext<DenialsDbContext>((sp, o) => o.UseNpgsql(
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Denials")
        ?? throw new InvalidOperationException("ConnectionStrings__Denials is not set.")));
builder.Services.AddSingleton(sp => IngestionOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<IngestionService>();
builder.Services.AddHttpClient<GeminiClient>((services, client) =>
    client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<GeminiOptions>().TimeoutSeconds));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AiCallGuard>();
builder.Services.AddScoped<AppealDraftService>();
builder.Services.AddSingleton(sp => ReferenceDataLoader.Load(sp.GetRequiredService<IngestionOptions>().DataDir));
builder.Services.AddHttpClient<IMailSender, GraphMailSender>((services, client) =>
    client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<GraphMailOptions>().TimeoutSeconds));
builder.Services.ConfigureHttpJsonOptions(o => JsonDefaults.Configure(o.SerializerOptions));
builder.Services.AddMemoryCache();
// Every error leaves the API in one shape (RFC 7807 problem details), including empty 403/404 results and
// unhandled exceptions, so the web app reads the message from `title` everywhere.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (context.Exception is DbUpdateConcurrencyException)
        context.ProblemDetails.Title = "Someone else changed this claim at the same time. Reload it and try again.";
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<AuthOptions>((options, auth) =>
    {
        options.Cookie.Name = "denials_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(auth.SessionHours);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = SessionStamp.ValidateAsync;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.Manager, policy => policy.RequireRole(Roles.Manager));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
builder.Services.AddOptions<RateLimiterOptions>().Configure<LimitsOptions>((options, limits) =>
{
    options.AddPolicy(RateLimits.Login, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.LoginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // AI calls, ingestion and the review queue are slow and costly; a few run at once and the rest wait in line,
    // so a burst of them cannot starve the database pool that every other request needs.
    options.AddConcurrencyLimiter(RateLimits.Expensive, limiter =>
    {
        limiter.PermitLimit = limits.ExpensiveConcurrentRequests;
        limiter.QueueLimit = limits.ExpensiveQueueLength!.Value; // [Required], so never null once validated
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

var app = builder.Build();

// A missing or out-of-range setting stops the app here, before it touches the database. Ingestion settings are
// read here too, because the startup ingestion below catches and logs its own failures.
app.Services.GetRequiredService<IStartupValidator>().Validate();
app.Services.GetRequiredService<IngestionOptions>();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex switch
    {
        BadHttpRequestException badRequest => badRequest.StatusCode,
        DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    },
});
app.UseStatusCodePages();
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DenialsDbContext>().Database.MigrateAsync();
    var auth = app.Services.GetRequiredService<AuthOptions>();
    if (string.IsNullOrWhiteSpace(auth.SeedPassword))
        app.Logger.LogWarning("Auth:SeedPassword is not set, so no users were created.");
    else
        await UserSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<DenialsDbContext>(), auth.SeedPassword);
    await UserSeeder.SyncEmailsAsync(scope.ServiceProvider.GetRequiredService<DenialsDbContext>(), auth.Emails);
    try
    {
        await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError("Startup ingestion failed; serving the last applied data. {Reason}", ex.Message);
    }
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapReconciliationEndpoints();
app.MapClaimEndpoints();
app.MapIngestionEndpoints();
app.MapDenialEndpoints();
app.MapWorklistEndpoints();
app.MapEvaluationEndpoints();
app.MapReferenceEndpoints();

app.Run();

public partial class Program;
