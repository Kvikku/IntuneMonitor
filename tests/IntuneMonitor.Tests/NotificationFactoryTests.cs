using IntuneMonitor.Config;
using IntuneMonitor.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Tests;

public class NotificationFactoryTests
{
    [Fact]
    public void Create_NoConfig_ReturnsEmptyList()
    {
        var config = new NotificationConfig();
        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Empty(senders);
    }

    [Fact]
    public void Create_TeamsConfigured_ReturnsTeamsSender()
    {
        var config = new NotificationConfig
        {
            Teams = new TeamsWebhookConfig { WebhookUrl = "https://teams.webhook.test/abc" }
        };

        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Single(senders);
        Assert.IsType<TeamsWebhookSender>(senders[0]);
        Assert.Equal("Microsoft Teams", senders[0].ChannelName);
    }

    [Fact]
    public void Create_SlackConfigured_ReturnsSlackSender()
    {
        var config = new NotificationConfig
        {
            Slack = new SlackWebhookConfig { WebhookUrl = "https://hooks.slack.com/test" }
        };

        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Single(senders);
        Assert.IsType<SlackWebhookSender>(senders[0]);
        Assert.Equal("Slack", senders[0].ChannelName);
    }

    [Fact]
    public void Create_EmailConfigured_ReturnsEmailSender()
    {
        var config = new NotificationConfig
        {
            Email = new EmailNotificationConfig
            {
                SmtpServer = "smtp.test.com",
                FromAddress = "noreply@test.com",
                ToAddresses = new List<string> { "admin@test.com" }
            }
        };

        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Single(senders);
        Assert.IsType<EmailNotificationSender>(senders[0]);
        Assert.Equal("Email", senders[0].ChannelName);
    }

    [Fact]
    public void Create_AllConfigured_ReturnsAllSenders()
    {
        var config = new NotificationConfig
        {
            Teams = new TeamsWebhookConfig { WebhookUrl = "https://teams.webhook.test/abc" },
            Slack = new SlackWebhookConfig { WebhookUrl = "https://hooks.slack.com/test" },
            Email = new EmailNotificationConfig
            {
                SmtpServer = "smtp.test.com",
                FromAddress = "noreply@test.com",
                ToAddresses = new List<string> { "admin@test.com" }
            }
        };

        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Equal(3, senders.Count);
    }

    [Fact]
    public void Create_EmptyWebhookUrl_SkipsSender()
    {
        var config = new NotificationConfig
        {
            Teams = new TeamsWebhookConfig { WebhookUrl = "" },
            Slack = new SlackWebhookConfig { WebhookUrl = "  " }
        };

        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Empty(senders);
    }

    [Fact]
    public void Create_EmailNoRecipients_SkipsSender()
    {
        var config = new NotificationConfig
        {
            Email = new EmailNotificationConfig
            {
                SmtpServer = "smtp.test.com",
                FromAddress = "noreply@test.com",
                ToAddresses = new List<string>()
            }
        };

        var senders = NotificationFactory.Create(config, NullLoggerFactory.Instance);

        Assert.Empty(senders);
    }
}
