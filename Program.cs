using CfMvc.Data;
using CfMvc.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache();

// ── Database ─────────────────────────────────────────────────────────────────
// Prefer the DATABASE_URL env var (set automatically by Replit's built-in PostgreSQL).
// Fall back to the ConnectionStrings:Default value in appsettings.json for local dev.
var rawConnStr = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrEmpty(rawConnStr))
    throw new InvalidOperationException(
        "No database connection string found. Set DATABASE_URL or ConnectionStrings:Default.");

// Npgsql requires key=value format; convert postgresql:// URIs (e.g. from Replit DATABASE_URL).
var connStr = ToNpgsqlConnectionString(rawConnStr);
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connStr, npg => npg.EnableRetryOnFailure(3)));

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddHttpClient<CodeforcesApiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("cf-explorer-mvc/1.0");
});

builder.Services.AddScoped<DatabaseQueryService>();
builder.Services.AddSingleton<EnrichmentService>();
builder.Services.AddHostedService<SyncService>();

// ── Host configuration ────────────────────────────────────────────────────────
// On Replit (and most cloud hosts) a $PORT env var is injected; bind 0.0.0.0 to it.
// In local dev, $PORT is unset and launchSettings.json / Kestrel defaults take over.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// ── Schema bootstrap ──────────────────────────────────────────────────────────
// EnsureCreated creates tables on first run without requiring a migration workflow.
// This is idempotent — existing schemas are left untouched.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseOutputCache();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>
/// Converts a postgresql:// or postgres:// URL to Npgsql's key=value connection string format.
/// If the input is already in key=value format it is returned unchanged.
/// </summary>
static string ToNpgsqlConnectionString(string url)
{
    if (!url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
     && !url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        return url;

    var uri = new Uri(url);
    var userParts = uri.UserInfo.Split(':', 2);
    var username = userParts.Length > 0 ? Uri.UnescapeDataString(userParts[0]) : "";
    var password = userParts.Length > 1 ? Uri.UnescapeDataString(userParts[1]) : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');

    var parts = new List<string>
    {
        $"Host={host}",
        $"Port={port}",
        $"Database={database}",
        $"Username={username}",
        $"Password={password}",
        "Trust Server Certificate=true",
    };

    // Forward recognised query params (sslmode, etc.).
    var query = uri.Query.TrimStart('?');
    if (!string.IsNullOrEmpty(query))
    {
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                parts.Add($"SSL Mode={val}");
        }
    }

    return string.Join(";", parts);
}
