using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using IntuneMonitor.Config;

namespace IntuneMonitor.Authentication;

/// <summary>
/// Creates an <see cref="TokenCredential"/> based on the authentication configuration.
/// Used by commands to obtain tokens for Microsoft Graph.
/// </summary>
public static class CredentialFactory
{
    /// <summary>Creates a credential from the provided configuration.</summary>
    /// <exception cref="InvalidOperationException">Thrown when required config is missing.</exception>
    public static TokenCredential Create(AuthenticationConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TenantId))
            throw new InvalidOperationException("Authentication.TenantId is required.");
        if (string.IsNullOrWhiteSpace(config.ClientId))
            throw new InvalidOperationException("Authentication.ClientId is required.");

        return config.Method?.ToLowerInvariant() switch
        {
            "certificate" => CreateCertificateCredential(config),
            "devicecode" => CreateDeviceCodeCredential(config),
            _ => CreateClientSecretCredential(config)
        };
    }

    private static ClientSecretCredential CreateClientSecretCredential(AuthenticationConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            throw new InvalidOperationException(
                "Authentication.ClientSecret is required when Method is 'ClientSecret'.");

        return new ClientSecretCredential(config.TenantId, config.ClientId, config.ClientSecret);
    }

    private static DeviceCodeCredential CreateDeviceCodeCredential(AuthenticationConfig config)
    {
        return new DeviceCodeCredential(new DeviceCodeCredentialOptions
        {
            TenantId = config.TenantId,
            ClientId = config.ClientId,
            DeviceCodeCallback = (code, cancellation) =>
            {
                Console.WriteLine();
                Console.WriteLine(code.Message);
                Console.WriteLine();
                return Task.CompletedTask;
            }
        });
    }

    private static ClientCertificateCredential CreateCertificateCredential(AuthenticationConfig config)
    {
        X509Certificate2 certificate;

        if (!string.IsNullOrWhiteSpace(config.CertificateThumbprint))
        {
            certificate = LoadCertificateFromStore(config.CertificateThumbprint);
        }
        else if (!string.IsNullOrWhiteSpace(config.CertificatePath))
        {
            certificate = string.IsNullOrWhiteSpace(config.CertificatePassword)
                ? new X509Certificate2(config.CertificatePath)
                : new X509Certificate2(config.CertificatePath, config.CertificatePassword,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
        }
        else
        {
            throw new InvalidOperationException(
                "Authentication: when Method is 'Certificate', either CertificateThumbprint or CertificatePath must be provided.");
        }

        return new ClientCertificateCredential(config.TenantId, config.ClientId, certificate);
    }

    private static X509Certificate2 LoadCertificateFromStore(string thumbprint)
    {
        foreach (var storeLocation in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(
                X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            if (certs.Count > 0)
                return certs[0];
        }

        throw new InvalidOperationException(
            $"Certificate with thumbprint '{thumbprint}' not found in CurrentUser\\My or LocalMachine\\My certificate stores.");
    }
}
