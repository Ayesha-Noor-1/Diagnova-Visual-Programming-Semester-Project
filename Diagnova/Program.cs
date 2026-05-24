using Diagnova.Data;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Driver;

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

// SQL Server for Identity/Auth (appsettings.json is gitignored — copy from appsettings.example.json)
const string DefaultSqlServerConnection =
    "Server=(localdb)\\mssqllocaldb;Database=DiagnovaAuth;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

static string ResolveSqlServerConnection(IConfiguration config)
{
    var cs = config.GetConnectionString("SqlServerConnection");
    return string.IsNullOrWhiteSpace(cs) ? DefaultSqlServerConnection : cs.Trim();
}

var sqlServerConnection = ResolveSqlServerConnection(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlServerConnection));

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
builder.Services.AddHttpClient<EmergencyPlacesService>();
builder.Services.AddSingleton<IEmergencyDetectorService, EmergencyDetectorService>();

// Email Service for emergency alerts
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddScoped<SosAlertService>();
builder.Services.AddScoped<DailyTipService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("SqlServerConnection")))
{
    app.Logger.LogWarning(
        "ConnectionStrings:SqlServerConnection is missing. Using LocalDB default. Copy appsettings.example.json to appsettings.json for full config.");
}

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

// API endpoints for Vitals
app.MapGet("/api/vitals", async (MongoDbService mongoDb, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var vitals = await mongoDb.VitalDefinitions.Find(v => v.UserId == userId).ToListAsync();
    return Results.Ok(vitals);
}).RequireAuthorization();

app.MapPost("/api/vitals", async (MongoDbService mongoDb, ClaimsPrincipal user, CreateVitalRequest request) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var vital = new VitalDefinition
    {
        UserId = userId,
        Name = request.Name,
        Unit = request.Unit,
        Readings = new List<VitalReadingEntry>()
    };
    await mongoDb.VitalDefinitions.InsertOneAsync(vital);
    return Results.Ok(vital);
}).RequireAuthorization();

app.MapPost("/api/vitals/reading", async (MongoDbService mongoDb, ClaimsPrincipal user, AddReadingRequest request) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var vital = await mongoDb.VitalDefinitions.Find(v => v.Id == request.VitalId && v.UserId == userId).FirstOrDefaultAsync();
    if (vital == null) return Results.NotFound();

    vital.Readings ??= new List<VitalReadingEntry>();
    vital.Readings.Add(new VitalReadingEntry
    {
        Value = request.Value,
        Note = request.Note,
        RecordedAt = DateTime.UtcNow
    });
    await mongoDb.VitalDefinitions.ReplaceOneAsync(v => v.Id == vital.Id, vital);
    return Results.Ok(vital);
}).RequireAuthorization();

// API endpoint for medicine search history
app.MapGet("/api/medicine/history", async (MongoDbService mongoDb, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var history = await mongoDb.MedicineSearchHistory
        .Find(h => h.UserId == userId)
        .SortByDescending(h => h.SearchedAt)
        .Limit(20)
        .ToListAsync();
    return Results.Ok(history);
}).RequireAuthorization();

app.MapGet("/api/medicine/search", async (string query, IOpenFdaService fda, MongoDbService mongoDb, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(query)) return Results.BadRequest(new { error = "Query is required." });

    var result = await fda.SearchDrugLabelAsync(query, ct);

    if (result is null)
        return Results.NotFound(new { searched = true, found = false, query });

    await mongoDb.MedicineSearchHistory.InsertOneAsync(new MongoMedicineSearch
    {
        UserId = userId,
        Query = query.Trim(),
        SearchedAt = DateTimeOffset.UtcNow,
        BrandName = result.BrandName ?? result.GenericName,
    }, cancellationToken: ct);

    return Results.Ok(new
    {
        searched = true,
        found = true,
        query,
        brandName = result.BrandName,
        genericName = result.GenericName,
        purpose = result.Purpose,
        dosageAndAdministration = result.DosageAndAdministration,
        warnings = result.Warnings,
        adverseReactions = result.AdverseReactions,
        contraindications = result.Contraindications,
    });
}).RequireAuthorization();

app.MapGet("/api/chat/sessions", async (MongoDbService mongoDb, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var sessions = await mongoDb.ChatSessions
        .Find(s => s.UserId == userId)
        .SortByDescending(s => s.StartedAt)
        .ToListAsync();

    var list = new List<object>();
    foreach (var session in sessions)
    {
        var messages = await mongoDb.ChatMessages
            .Find(m => m.SessionId == session.Id)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        var firstUser = messages.FirstOrDefault(m => m.Role == "user");
        var title = firstUser != null
            ? (firstUser.Content.Length > 48 ? firstUser.Content[..48] + "…" : firstUser.Content)
            : "New conversation";

        list.Add(new
        {
            id = session.Id,
            title,
            startedAt = session.StartedAt,
            closedAt = session.ClosedAt,
            messageCount = messages.Count,
        });
    }

    return Results.Ok(list);
}).RequireAuthorization();

app.MapGet("/api/tips/daily", async (DailyTipService tips, ClaimsPrincipal user, CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var tip = await tips.GetTodayTipAsync(userId, ct);
    return Results.Ok(tip);
}).RequireAuthorization();

app.MapGet("/api/dashboard/activity", async (MongoDbService mongoDb, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var sessionIds = await mongoDb.ChatSessions
        .Find(s => s.UserId == userId)
        .Project(s => s.Id)
        .ToListAsync();
    var idList = sessionIds.Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToList();

    var labels = new List<string>();
    var data = new List<int>();
    for (var i = 6; i >= 0; i--)
    {
        var day = DateTimeOffset.UtcNow.Date.AddDays(-i);
        labels.Add(day.ToString("ddd"));
        var next = day.AddDays(1);
        var count = idList.Count == 0
            ? 0
            : await mongoDb.ChatMessages
                .Find(m => idList.Contains(m.SessionId) && m.CreatedAt >= day && m.CreatedAt < next)
                .CountDocumentsAsync();
        data.Add((int)count);
    }

    return Results.Ok(new { labels, data });
}).RequireAuthorization();

app.MapPost("/api/profile/update", async (MongoDbService mongoDb, ClaimsPrincipal user, UpdateProfileRequest request) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var profile = await mongoDb.MedicalProfiles.Find(p => p.UserId == userId).FirstOrDefaultAsync();
    if (profile == null) return Results.NotFound();

    switch (request.Field?.ToLower())
    {
        case "fullname":
            profile.FullName = request.Value ?? string.Empty;
            break;
        case "username":
            profile.Username = request.Value ?? string.Empty;
            break;
        case "email":
            profile.Email = request.Value ?? string.Empty;
            break;
        case "gender":
            profile.Gender = request.Value ?? string.Empty;
            break;
        case "weightkg":
            if (double.TryParse(request.Value, out var weight))
                profile.WeightKg = weight;
            break;
        case "heightcm":
            if (double.TryParse(request.Value, out var height))
                profile.HeightCm = height;
            break;
        case "bloodtype":
            profile.BloodType = request.Value ?? string.Empty;
            break;
        case "currentmedications":
            profile.CurrentMedications = request.Value ?? string.Empty;
            break;
        case "smokinghabit":
            profile.SmokingHabit = request.Value ?? "Never";
            break;
        case "alcoholhabit":
            profile.AlcoholHabit = request.Value ?? "Never";
            break;
        case "primarygoal":
            profile.PrimaryGoal = request.Value ?? string.Empty;
            break;
        case "activitylevel":
            profile.ActivityLevel = request.Value ?? "Sedentary";
            break;
        case "dailycalorietarget":
            if (int.TryParse(request.Value, out var calories))
                profile.DailyCalorieTarget = calories;
            break;
        case "countryregion":
            profile.CountryRegion = request.Value ?? string.Empty;
            break;
        case "emergencycontactname":
            profile.EmergencyContactName = request.Value ?? string.Empty;
            break;
        case "emergencycontactemail":
            profile.EmergencyContactEmail = request.Value ?? string.Empty;
            break;
        case "emergencycontactphone":
            profile.EmergencyContactPhone = request.Value ?? string.Empty;
            break;
        case "preexistingconditions":
            profile.PreExistingConditions = request.Value?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>();
            break;
        case "allergies":
            profile.Allergies = request.Value?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>();
            break;
        default:
            return Results.BadRequest(new { error = "Unknown field." });
    }

    profile.UpdatedAt = DateTime.UtcNow;
    await mongoDb.MedicalProfiles.ReplaceOneAsync(p => p.Id == profile.Id, profile);
    return Results.Ok(new { success = true });
}).RequireAuthorization();

// One-tap SOS — location + medical profile to emergency contacts
app.MapPost("/api/sos/trigger", async (SosAlertService sos, ClaimsPrincipal user, SosTriggerRequest request, CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var result = await sos.TriggerAsync(
        userId,
        request.Latitude,
        request.Longitude,
        string.IsNullOrWhiteSpace(request.Note) ? "One-tap SOS — user needs immediate help." : request.Note.Trim(),
        ct);

    if (!result.Ok)
        return Results.BadRequest(result);

    return Results.Ok(result);
}).RequireAuthorization();

// Emergency Email API endpoint
app.MapPost("/api/emergency/send-email", async (MongoDbService mongoDb, IEmailService emailService, ClaimsPrincipal user, EmergencyEmailRequest request) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var profile = await mongoDb.MedicalProfiles.Find(p => p.UserId == userId).FirstOrDefaultAsync();
    if (profile == null) return Results.NotFound(new { error = "Profile not found" });

    if (string.IsNullOrEmpty(profile.EmergencyContactEmail))
        return Results.BadRequest(new { error = "No emergency contact email configured" });

    var success = await emailService.SendEmergencyAlertAsync(
        profile.EmergencyContactEmail,
        profile.EmergencyContactName ?? "Emergency Contact",
        profile.FullName,
        request.Message,
        request.Keywords ?? new List<string>(),
        request.AlarmingReason
    );

    return Results.Ok(new { success = success });
}).RequireAuthorization();

// ===== ADD THESE TWO ENDPOINTS FOR REGISTRATION AVAILABILITY CHECKS =====
app.MapGet("/api/check-username", async (string username, UserManager<ApplicationUser> userManager) =>
{
    if (string.IsNullOrWhiteSpace(username))
        return Results.Ok(new { available = false });

    var existing = await userManager.FindByNameAsync(username.Trim());
    return Results.Ok(new { available = existing == null });
});

app.MapGet("/api/check-email", async (string email, UserManager<ApplicationUser> userManager) =>
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.Ok(new { available = false });

    var existing = await userManager.FindByEmailAsync(email.Trim());
    return Results.Ok(new { available = existing == null });
});

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

// Record types for API endpoints
public record UpdateProfileRequest(string Field, string? Value);
public record CreateVitalRequest(string Name, string Unit);
public record AddReadingRequest(string VitalId, double Value, string? Note);
public record EmergencyEmailRequest(string Message, List<string>? Keywords, string? AlarmingReason);