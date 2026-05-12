using Diagnova.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Diagnova.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Note: ChatSessions, ChatMessages, VitalReadings are now in MongoDB
    // These DbSets are no longer used but kept for reference
    // You can remove them if desired
}