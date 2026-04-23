# Local TLS certificates for reverse proxy setup

This folder is intentionally kept out of source control except for this guide.

## Required files

- `certs/nginx/tls.crt`
- `certs/nginx/tls.key`
- `certs/api/api.pfx`

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

## Environment variable

Create a local `.env` file in the repository root:

```text
API_CERT_PASSWORD=change-me
```

Use the same value as used in the `dotnet dev-certs` export command.

