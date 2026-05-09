using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diagnova.Pages;

[Authorize]
public class NearbyModel : PageModel
{
    public void OnGet()
    {
    }
}
