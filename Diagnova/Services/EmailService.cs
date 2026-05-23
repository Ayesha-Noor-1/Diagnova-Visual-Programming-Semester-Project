using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Diagnova.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmergencyAlertAsync(
        string toEmail,
        string toName,
        string userName,
        string userMessage,
        List<string> keywords,
        string? alarmingReason = null)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = _configuration["Email:SmtpPort"];
            var smtpUser = _configuration["Email:SmtpUser"];
            var smtpPassword = _configuration["Email:SmtpPassword"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email configuration is incomplete. Skipping email send.");
                return false;
            }

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Diagnova Health Alert", smtpUser));
            email.To.Add(new MailboxAddress(toName, toEmail));
            email.Subject = $"🚨 EMERGENCY ALERT: {userName} needs immediate medical attention";

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; }}
        .alert-container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .alert-header {{ background: linear-gradient(135deg, #dc3545, #c82333); color: white; padding: 20px; border-radius: 15px 15px 0 0; text-align: center; }}
        .alert-content {{ background: #f8f9fa; padding: 25px; border-radius: 0 0 15px 15px; }}
        .keyword-badge {{ background: #ffc107; color: #856404; padding: 5px 12px; border-radius: 20px; display: inline-block; margin: 3px; font-size: 12px; }}
        .emergency-box {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0; border-radius: 8px; }}
        .action-list {{ background: white; padding: 15px; border-radius: 10px; margin: 15px 0; }}
        .action-list li {{ margin: 8px 0; }}
        hr {{ border: none; border-top: 1px solid #dee2e6; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='alert-container'>
        <div class='alert-header'>
            <h2>⚠️ EMERGENCY ALERT</h2>
            <p>Immediate medical attention required</p>
        </div>
        <div class='alert-content'>
            <h3>🚑 Patient Information</h3>
            <p><strong>Name:</strong> {System.Net.WebUtility.HtmlEncode(userName)}</p>
            <p><strong>Time:</strong> {DateTime.Now:MMMM dd, yyyy - hh:mm tt}</p>
            
            <div class='emergency-box'>
                <strong>⚠️ Reported Emergency:</strong>
                <p style='margin-top: 10px;'>{System.Net.WebUtility.HtmlEncode(userMessage)}</p>
            </div>
            {(alarmingReason != null ? $@"
            <div class='emergency-box'>
                <strong>📋 AI Assessment:</strong>
                <p style='margin-top: 10px;'>{System.Net.WebUtility.HtmlEncode(alarmingReason)}</p>
            </div>" : "")}
            
            <h3>🔍 Detected Keywords</h3>
            <div>
                {string.Join("", keywords.Select(k => $"<span class='keyword-badge'>{System.Net.WebUtility.HtmlEncode(k)}</span>"))}
            </div>
            
            <div class='action-list'>
                <h3>📞 Recommended Actions</h3>
                <ul>
                    <li><strong>Call emergency services immediately</strong> - Dial 1122 (Pakistan)</li>
                    <li>Ask them to share their current location</li>
                    <li>Guide them to the nearest hospital</li>
                    <li>Stay on the line until help arrives</li>
                </ul>
            </div>
            
            <hr>
            <p style='font-size: 12px; color: #6c757d; text-align: center;'>
                This is an automated alert from Diagnova Health Platform.<br>
                Please take immediate action.
            </p>
        </div>
    </div>
</body>
</html>";

            email.Body = new TextPart("html") { Text = body };

            // USE FULLY QUALIFIED NAME to avoid ambiguity
            using var client = new MailKit.Net.Smtp.SmtpClient();

            var port = !string.IsNullOrEmpty(smtpPort) ? int.Parse(smtpPort) : 587;
            await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPassword);
            await client.SendAsync(email);
            await client.DisconnectAsync(true);

            _logger.LogInformation($"Emergency email sent to {toEmail} for user {userName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send emergency email to {toEmail}");
            return false;
        }
    }
}