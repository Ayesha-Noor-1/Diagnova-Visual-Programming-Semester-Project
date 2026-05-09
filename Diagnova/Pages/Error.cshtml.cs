using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diagnova.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>Populated in Development when re-executing from the exception handler.</summary>
    public string? ExceptionDetail { get; private set; }

    public bool ShowDevDetails => !string.IsNullOrEmpty(ExceptionDetail);

    /// <summary>Short message shown outside Development (no stack trace).</summary>
    public string? ErrorMessage { get; private set; }

    private readonly ILogger<ErrorModel> _logger;
    private readonly IWebHostEnvironment _env;

    public ErrorModel(ILogger<ErrorModel> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error is { } ex)
        {
            _logger.LogError(ex, "Unhandled exception at {Path}", feature.Path);

            if (_env.IsDevelopment())
                ExceptionDetail = ex.ToString();
            else
                ErrorMessage = ex.Message;
        }
    }
}

