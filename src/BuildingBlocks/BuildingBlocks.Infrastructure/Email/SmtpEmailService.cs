using BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BuildingBlocks.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
  private readonly IConfiguration _config;
  private readonly ILogger<SmtpEmailService> _logger;

  public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
  {
    _config = config;
    _logger = logger;
  }

  public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
  {
    var host = _config["Email:Host"];
    if (string.IsNullOrEmpty(host))
    {
      _logger.LogInformation("Email not configured — skipping send to {To}: {Subject}", to, subject);
      return;
    }

    try
    {
      var message = new MimeMessage();
      message.From.Add(MailboxAddress.Parse(_config["Email:From"] ?? "noreply@crm-saas.com"));
      message.To.Add(MailboxAddress.Parse(to));
      message.Subject = subject;
      message.Body = new TextPart("html") { Text = htmlBody };

      using var client = new MailKit.Net.Smtp.SmtpClient();

      var port = int.Parse(_config["Email:Port"] ?? "587");
      var useSsl = bool.Parse(_config["Email:UseSsl"] ?? "true");

      await client.ConnectAsync(host, port, useSsl ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None, ct);

      var user = _config["Email:User"];
      var password = _config["Email:Password"];
      if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
      {
        await client.AuthenticateAsync(user, password, ct);
      }

      await client.SendAsync(message, ct);
      await client.DisconnectAsync(true, ct);

      _logger.LogInformation("Email sent successfully to {To}", to);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send email to {To}", to);
    }
  }
}