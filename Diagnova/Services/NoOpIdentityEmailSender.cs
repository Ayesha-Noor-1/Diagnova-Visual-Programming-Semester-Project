using Diagnova.Models;
using Microsoft.AspNetCore.Identity;

namespace Diagnova.Services;

/// <summary>
/// Identity UI calls these methods during registration and password flows even when email confirmation is disabled.
/// Without a registered implementation, DI fails at runtime when processing registration.
/// </summary>
public sealed class NoOpIdentityEmailSender : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        Task.CompletedTask;

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        Task.CompletedTask;
}
