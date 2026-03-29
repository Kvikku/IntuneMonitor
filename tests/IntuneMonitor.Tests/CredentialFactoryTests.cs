using IntuneMonitor.Config;
using IntuneMonitor.Authentication;
using Azure.Identity;

namespace IntuneMonitor.Tests;

public class CredentialFactoryTests
{
    [Fact]
    public void Create_ClientSecret_ReturnsClientSecretCredential()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            Method = "ClientSecret",
            ClientSecret = "secret-value"
        };

        var credential = CredentialFactory.Create(config);

        Assert.IsType<ClientSecretCredential>(credential);
    }

    [Fact]
    public void Create_DeviceCode_ReturnsDeviceCodeCredential()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            Method = "DeviceCode"
        };

        var credential = CredentialFactory.Create(config);

        Assert.IsType<DeviceCodeCredential>(credential);
    }

    [Fact]
    public void Create_MissingTenantId_ThrowsInvalidOperation()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "",
            ClientId = "client-id",
            Method = "ClientSecret",
            ClientSecret = "secret"
        };

        Assert.Throws<InvalidOperationException>(() => CredentialFactory.Create(config));
    }

    [Fact]
    public void Create_MissingClientId_ThrowsInvalidOperation()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "tenant-id",
            ClientId = "",
            Method = "ClientSecret",
            ClientSecret = "secret"
        };

        Assert.Throws<InvalidOperationException>(() => CredentialFactory.Create(config));
    }

    [Fact]
    public void Create_ClientSecretMissing_ThrowsInvalidOperation()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            Method = "ClientSecret",
            ClientSecret = ""
        };

        Assert.Throws<InvalidOperationException>(() => CredentialFactory.Create(config));
    }

    [Fact]
    public void Create_CertificateNoPathOrThumbprint_ThrowsInvalidOperation()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            Method = "Certificate"
        };

        Assert.Throws<InvalidOperationException>(() => CredentialFactory.Create(config));
    }

    [Fact]
    public void Create_DefaultMethod_ReturnsClientSecretCredential()
    {
        var config = new AuthenticationConfig
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "secret"
        };

        var credential = CredentialFactory.Create(config);

        Assert.IsType<ClientSecretCredential>(credential);
    }
}
