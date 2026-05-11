# Local TLS certificates for reverse proxy setup

This folder is intentionally kept out of source control except for this guide.

## Required files

- `certs/nginx/tls.crt`
- `certs/nginx/tls.key`
- `certs/api/api.pfx`

Optional, for peer mTLS testing:

- a client certificate imported into the current user or local machine certificate store

## Generate local development certificates

Use OpenSSL to create an edge certificate for Nginx:

```powershell
New-Item -ItemType Directory -Force -Path certs\nginx | Out-Null
openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout certs\nginx\tls.key -out certs\nginx\tls.crt -subj "/CN=obfuscal.local"
```

Use `dotnet dev-certs` for the API PFX consumed by Kestrel:

```powershell
New-Item -ItemType Directory -Force -Path certs\api | Out-Null
dotnet dev-certs https -ep certs\api\api.pfx -p "change-me"
```

## Generate a peer client certificate for mTLS testing

If you want to test client-certificate selection locally, create a separate client certificate, export it to PFX,
and import it into the certificate store used by the ObfusCal process.

Example (self-signed client certificate):

```powershell
$clientCert = New-SelfSignedCertificate -DnsName "obfuscal-peer-client" -CertStoreLocation "Cert:\CurrentUser\My"
$clientPassword = ConvertTo-SecureString -String "change-me" -Force -AsPlainText
Export-PfxCertificate -Cert $clientCert -FilePath certs\api\peer-client.pfx -Password $clientPassword
```

To obtain the thumbprint that must be entered into `PeerConnections.ClientCertificateThumbprint`:

```powershell
Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -like "*obfuscal-peer-client*" | Select-Object Subject, Thumbprint
```

Copy the thumbprint exactly as shown. The application normalizes whitespace and separators, but keeping the canonical
thumbprint format makes operator handoff easier.

## Environment variable

Create a local `.env` file in the repository root:

```text
API_CERT_PASSWORD=change-me
```

Use the same value as used in the `dotnet dev-certs` export command.

For development peer transport security, also set `PEERTRANSPORTSECURITY__ALLOWSELFSIGNEDCERTS=true` in your local
`.env` or user environment if you are using self-signed peer certificates.

