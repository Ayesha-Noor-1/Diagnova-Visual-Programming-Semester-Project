using Microsoft.AspNetCore.Identity;

namespace Diagnova.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
