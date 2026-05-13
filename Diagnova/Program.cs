using Diagnova.Data;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// When the working directory or base path is bin/Debug/net10.0, ASP.NET looks for wwwroot there and fails.
// Walk up until we find a directory that contains wwwroot (project folder when developing, or publish output).
static string ResolveProjectContentRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var dir = new DirectoryInfo(Path.GetFullPath(start));
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "wwwroot")))
                return dir.FullName;
            dir = dir.Parent;
        }
    }

    return Directory.GetCurrentDirectory();
}

static bool HasHttpsEndpoint() =>
    (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty)
    .Contains("https:", StringComparison.OrdinalIgnoreCase);

var contentRoot = ResolveProjectContentRoot();
var webRoot = Path.Combine(contentRoot, "wwwroot");
if (!Directory.Exists(webRoot))
{
    throw new DirectoryNotFoundException(
        $"Static files folder not found: {webRoot}. Ensure wwwroot exists next to Diagnova.csproj.");
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = webRoot,
});

// Default host loads user secrets only in Development; VS/IIS profiles sometimes omit that.
// Always merge secrets from this assembly (same UserSecretsId as `dotnet user-secrets`) so OpenAI:ApiKey is found.
builder.Configuration.AddUserSecrets(typeof(AppDbContext).Assembly, optional: true);

// Lets MapStaticAssets / library bundles resolve when not running from `dotnet publish` output (e.g. Production env + dotnet run).
builder.WebHost.UseStaticWebAssets();

// SQL Server for Identity/Auth
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerConnection")));

// MongoDB settings
builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("MongoDbConnection") ?? "mongodb://localhost:27017";
    options.DatabaseName = builder.Configuration["MongoDbSettings:DatabaseName"] ?? "DiagnovaMedicalDb";
    options.ProfilesCollectionName = builder.Configuration["MongoDbSettings:ProfilesCollectionName"] ?? "MedicalProfiles";
    options.ChatSessionsCollectionName = builder.Configuration["MongoDbSettings:ChatSessionsCollectionName"] ?? "ChatSessions";
    options.ChatMessagesCollectionName = builder.Configuration["MongoDbSettings:ChatMessagesCollectionName"] ?? "ChatMessages";
    options.VitalReadingsCollectionName = builder.Configuration["MongoDbSettings:VitalReadingsCollectionName"] ?? "VitalReadings";
});

builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>();

// Identity UI calls IEmailSender<TUser> during registration even when email confirmation is disabled.
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, NoOpIdentityEmailSender>();

builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.PostConfigure<OpenAiOptions>(o =>
{
    if (!string.IsNullOrEmpty(o.ApiKey))
        o.ApiKey = o.ApiKey.Trim();
    if (!string.IsNullOrEmpty(o.BaseUrl))
        o.BaseUrl = o.BaseUrl.Trim();
    if (!string.IsNullOrEmpty(o.Model))
        o.Model = o.Model.Trim();
    if (!string.IsNullOrEmpty(o.EmergencyRoutingModel))
        o.EmergencyRoutingModel = o.EmergencyRoutingModel.Trim();
});
builder.Services.AddHttpClient<IOpenAiChatService, OpenAiChatService>();
builder.Services.AddHttpClient<IOpenFdaService, OpenFdaService>();
builder.Services.AddSingleton<IEmergencyDetectorService, EmergencyDetectorService>();

var app = builder.Build();

{
    var open = app.Configuration.GetSection(OpenAiOptions.SectionName);
    var hasKey = !string.IsNullOrWhiteSpace(open["ApiKey"]);
    var baseUrl = string.IsNullOrWhiteSpace(open["BaseUrl"]) ? "(default https://api.openai.com/v1/)" : open["BaseUrl"]!;
    var model = string.IsNullOrWhiteSpace(open["Model"]) ? "(default)" : open["Model"]!;
    var route = string.IsNullOrWhiteSpace(open["EmergencyRoutingModel"]) ? "(same as Model)" : open["EmergencyRoutingModel"]!;
    app.Logger.LogInformation(
        "OpenAI config: ApiKey configured={HasKey}; BaseUrl={Base}; Model={Model}; EmergencyRoutingModel={Route}",
        hasKey, baseUrl, model, route);

    static bool LooksLikeUrlInsteadOfModel(string? m)
    {
        if (string.IsNullOrWhiteSpace(m)) return false;
        m = m.Trim();
        if (m.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || m.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;
        if (m.Equals("api/v1", StringComparison.OrdinalIgnoreCase) || m.Equals("v1", StringComparison.OrdinalIgnoreCase))
            return true;
        if (m.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
            return true;
        return m.Contains("/api/", StringComparison.Ordinal);
    }

    if (LooksLikeUrlInsteadOfModel(open["Model"]))
    {
        app.Logger.LogWarning(
            "OpenAI:Model is {Model} — this looks like a URL fragment, not a model id. Set OpenAI:BaseUrl to your API root (e.g. https://openrouter.ai/api/v1/) and OpenAI:Model to a slug from https://openrouter.ai/models (e.g. openai/gpt-4o-mini).",
            open["Model"]);
    }

    if (LooksLikeUrlInsteadOfModel(open["EmergencyRoutingModel"]))
    {
        app.Logger.LogWarning(
            "OpenAI:EmergencyRoutingModel is {Model} — use a real model slug, not a URL. See https://openrouter.ai/models",
            open["EmergencyRoutingModel"]);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // Only advertise HSTS when we actually serve HTTPS (avoid confusing HTTP-only runs).
    if (HasHttpsEndpoint())
        app.UseHsts();
}

// If nothing listens on HTTPS, redirecting HTTP→HTTPS breaks requests for CSS/JS (307 to a dead port).
if (HasHttpsEndpoint())
    app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

// Apply SQL Server migrations for Identity
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();