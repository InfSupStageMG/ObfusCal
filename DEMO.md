# ObfusCal Peer Demo — Setup Guide

Two ObfusCal instances running on one machine, demonstrating cross-domain availability sync.

- **Instance A** → `https://localhost`
- **Instance B** → `https://localhost:4443`

Both share the same Entra app registration. Peer-to-peer auth uses independent API keys,
completely separate from Entra.

---

## Prerequisites

- Podman Desktop running
- Certificates generated under `certs/nginx/` and `certs/api/` (see `certs/README.md`)
- `.env.peer-demo` filled in from `.env.peer-demo.example`

### Generate encryption keys

```powershell
# Run twice — once for KEY_A, once for KEY_B
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Do the same for `SYNC__APIKEY_A` and `SYNC__APIKEY_B`. All four values must be independent.

### Entra app registration

Add both redirect URIs to the existing app registration (Web platform):

- `https://localhost/signin-oidc`
- `https://localhost:4443/signin-oidc`

---

## Starting the stack

```powershell
podman compose -f docker-compose.peer-demo.yaml --env-file .env.peer-demo up -d --build
```

Both instances share the same image build. First startup takes a few minutes.

Check status:

```powershell
podman ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

All six containers should eventually show `Up ... (healthy)` or `Up ...`:
`db-a`, `db-b`, `api-a`, `api-b`, `proxy-a`, `proxy-b`.

### If a proxy container exits immediately

The nginx config files must be saved without a UTF-8 BOM. Run this after any edit:

```powershell
foreach ($f in @("nginx-peer-a.conf", "nginx-peer-b.conf")) {
    $path = "C:\Users\GijsP\RiderProjects\ObfusCal\$f"
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
        [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Stripped BOM from $f"
    }
}
podman restart obfuscal-proxy-a-1 obfuscal-proxy-b-1
```

Also be sure to check the line separators are CRLF.

---

## Wiring the peer connections

Both instances need a sysadmin account. Assign the `Sysadmin` app role in Entra to your
account — the same account works for both instances.

Open both instances in separate browser tabs and accept the self-signed certificate warning
on each.

### On Instance A (`https://localhost`) — add Instance B as a peer

Navigate to **Peer Connections** (`/peers`).

| Field        | Value                                           |
|--------------|-------------------------------------------------|
| Instance ID  | `instance-b`                                    |
| Base Address | `https://proxy-b`                               |
| API Key      | value of `SYNC__APIKEY_B` from `.env.peer-demo` |

> The Base Address must be the Docker service name (`proxy-b`), not `localhost:4443`.
> Containers reach each other by service name inside the compose network.

> The API Key is the credential that **B presents when calling A**. A stores its hash to
> verify inbound sync requests from B.

### On Instance B (`https://localhost:4443`) — add Instance A as a peer

Navigate to **Peer Connections** (`/peers`).

| Field        | Value                                           |
|--------------|-------------------------------------------------|
| Instance ID  | `instance-a`                                    |
| Base Address | `https://proxy-a`                               |
| API Key      | value of `SYNC__APIKEY_A` from `.env.peer-demo` |

---

## Triggering sync

The background sync runs every 15 minutes (`Sync__SyncIntervalSeconds=900`). To see results
sooner, add a calendar source and calendar owner on each instance, link an owner to the peer
connection, then wait for the next sync cycle or restart the API container to trigger an
immediate run.

Check sync status under **Sync Status** (`/sync-status`) on either instance.

Logs for the sync job:

```powershell
podman compose -f docker-compose.peer-demo.yaml --env-file .env.peer-demo logs api-a --tail 50
podman compose -f docker-compose.peer-demo.yaml --env-file .env.peer-demo logs api-b --tail 50
```

---

## Stopping the stack

```powershell
podman compose -f docker-compose.peer-demo.yaml --env-file .env.peer-demo down
```

Add `-v` to also remove the database volumes (full reset):

```powershell
podman compose -f docker-compose.peer-demo.yaml --env-file .env.peer-demo down -v
```

