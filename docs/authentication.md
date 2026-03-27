# Authentication

IntuneMonitor supports two authentication methods against Microsoft Entra ID. Both use app-only (daemon) credentials — no user sign-in required.

## Client Secret

The simplest method. Create a secret in your Entra app registration.

### appsettings.json

```json
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "ClientSecret",
    "ClientSecret": "your-secret-here"
  }
}
```

### Environment Variables

```bash
export INTUNEMONITOR_Authentication__Method="ClientSecret"
export INTUNEMONITOR_Authentication__ClientSecret="your-secret-here"
```

### CLI Flags

```bash
dotnet run -- export --client-secret "your-secret-here"
```

> [!NOTE]
> Setting `--client-secret` automatically switches the method to `ClientSecret`.

---

## Certificate (PFX File)

More secure than client secrets. Upload the public key to your Entra app registration and reference the private key locally.

### appsettings.json

```json
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "Certificate",
    "CertificatePath": "/path/to/cert.pfx",
    "CertificatePassword": "optional-password"
  }
}
```

### CLI Flags

```bash
dotnet run -- export --cert-path ./cert.pfx --cert-password "my-password"
```

---

## Certificate (Windows Certificate Store)

Reference a certificate already installed in the Windows certificate store by its thumbprint. Useful on servers and Azure VMs where certificates are deployed via policy.

### appsettings.json

```json
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "Certificate",
    "CertificateThumbprint": "AABBCCDDEEFF00112233..."
  }
}
```

### CLI Flags

```bash
dotnet run -- export --cert-thumbprint "AABBCCDDEEFF00112233..."
```

The app searches both `CurrentUser\My` and `LocalMachine\My` stores.

---

## Priority

When multiple credentials are provided, the CLI flags take precedence:

```
CLI flags  >  Environment variables  >  appsettings.json
```

Setting `--client-secret` automatically switches to `ClientSecret` method. Setting `--cert-path` or `--cert-thumbprint` automatically switches to `Certificate` method.

## Creating an Entra App Registration

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name: `IntuneMonitor` (or any name)
3. Supported account types: **Accounts in this organizational directory only**
4. Register
5. Note the **Application (client) ID** and **Directory (tenant) ID**
6. Go to **API permissions** → **Add a permission** → **Microsoft Graph** → **Application permissions**
7. Add the permissions from [Getting Started — API Permissions](getting-started.md#required-api-permissions)
8. Click **Grant admin consent**
9. Create a **Client secret** or upload a **Certificate** under **Certificates & secrets**
