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

builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.AddHttpClient<IOpenAiChatService, OpenAiChatService>();
builder.Services.AddHttpClient<IOpenFdaService, OpenFdaService>();
builder.Services.AddSingleton<IEmergencyDetectorService, EmergencyDetectorService>();

var app = builder.Build();

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