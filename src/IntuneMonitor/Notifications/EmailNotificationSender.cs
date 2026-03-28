using System.Net;
using System.Net.Mail;
using System.Text;

namespace IntuneMonitor.Notifications;

using IntuneMonitor.Config;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sends drift detection notifications via email using SMTP.
/// The email body is an HTML-formatted summary of detected changes.
/// </summary>
public class EmailNotificationSender : INotificationSender
{
    private readonly EmailNotificationConfig _config;
    private readonly ILogger<EmailNotificationSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationSender"/> class.
    /// </summary>
    /// <param name="config">Email notification configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public EmailNotificationSender(EmailNotificationConfig config, ILogger<EmailNotificationSender> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ChannelName => "Email";

    /// <inheritdoc />
    public async Task SendAsync(ChangeReport report, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Building email notification for {ChangeCount} change(s)", report.TotalCount);

        var subject = $"IntuneMonitor Alert: {report.TotalCount} change(s) detected";
        var body = BuildHtmlBody(report);

        using var message = new MailMessage
        {
            From = new MailAddress(_config.FromAddress),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        foreach (var to in _config.ToAddresses)
        {
            message.To.Add(new MailAddress(to));
        }

        using var client = new SmtpClient(_config.SmtpServer, _config.SmtpPort)
        {
            EnableSsl = _config.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_config.Username))
        {
            client.Credentials = new NetworkCredential(_config.Username, _config.Password);
        }

        await client.SendMailAsync(message, cancellationToken);

        _logger.LogDebug("Email notification sent to {RecipientCount} recipient(s)", _config.ToAddresses.Count);
    }

    // ----------------------------------------------------------------
    // Private helpers
    // ----------------------------------------------------------------

    private static string BuildHtmlBody(ChangeReport report)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><style>");
        html.AppendLine("body { font-family: Segoe UI, Arial, sans-serif; font-size: 14px; color: #333; }");
        html.AppendLine("h2 { color: #0078D4; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; margin-top: 12px; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #0078D4; color: #fff; }");
        html.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
        html.AppendLine(".added { color: #107C10; } .removed { color: #D13438; } .modified { color: #CA5010; }");
        html.AppendLine("</style></head><body>");

        html.AppendLine("<h2>IntuneMonitor &ndash; Configuration Drift Detected</h2>");
        html.AppendLine($"<p><strong>{report.TotalCount}</strong> change(s) detected in tenant " +
                         $"<strong>{Encode(report.TenantName)}</strong> at {report.GeneratedAt:u}.</p>");

        // Summary table
        html.AppendLine("<table><tr><th>Added</th><th>Removed</th><th>Modified</th><th>Total</th></tr>");
        html.AppendLine($"<tr><td>{report.AddedCount}</td><td>{report.RemovedCount}</td>" +
                         $"<td>{report.ModifiedCount}</td><td>{report.TotalCount}</td></tr></table>");

        // Change details (up to 20)
        var displayChanges = report.Changes.Take(20).ToList();
        if (displayChanges.Count > 0)
        {
            html.AppendLine("<h3>Change Details</h3>");
            html.AppendLine("<table><tr><th>Type</th><th>Content Type</th><th>Policy Name</th><th>Severity</th></tr>");

            foreach (var change in displayChanges)
            {
                var cssClass = change.ChangeType switch
                {
                    ChangeType.Added => "added",
                    ChangeType.Removed => "removed",
                    ChangeType.Modified => "modified",
                    _ => ""
                };

                html.AppendLine($"<tr><td class=\"{cssClass}\">{change.ChangeType}</td>" +
                                 $"<td>{Encode(change.ContentType)}</td>" +
                                 $"<td>{Encode(change.PolicyName)}</td>" +
                                 $"<td>{change.Severity}</td></tr>");
            }

            html.AppendLine("</table>");

            if (report.TotalCount > 20)
            {
                html.AppendLine($"<p><em>... and {report.TotalCount - 20} more change(s).</em></p>");
            }
        }

        html.AppendLine("</body></html>");

        return html.ToString();
    }

    private static string Encode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
