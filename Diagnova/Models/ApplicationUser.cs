using Microsoft.AspNetCore.Identity;

namespace Diagnova.Models;

public class ApplicationUser : IdentityUser
{
    // The base IdentityUser already has UserName and Email
    // We don't need an extra Username property - it will cause conflicts
    // Just use the existing UserName property for the username

    // You can add other non-medical properties here if needed
    // But medical data goes to MongoDB
}