# Business OS - backend

Multi-tenant B2B CRM API (.NET 10, EF Core, PostgreSQL). Solution: `apps/api/BusinessOS.sln`
(Domain, Application, Infrastructure, WebApi, Tests).

## Configuration and secrets

**No secret is committed.** `apps/api/WebApi/appsettings.json` only holds non-secret defaults and
empty placeholders. Real values come from:

| Where | Used for |
|---|---|
| `dotnet user-secrets` | Local development |
| Environment variables | Production / CI |

The app refuses to start (`Invalid configuration: ...`) when a required key is missing.

### Local setup

```powershell
cd apps/api/WebApi
# Set keys one by one (see user-secrets.example.json for the full list):
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=5432;Database=project-db;User Id=postgres;Password=..."
dotnet user-secrets set "JWT_ACCESS_SECRET" "<64 hex chars>"
dotnet user-secrets set "JWT_REFRESH_SECRET" "<64 hex chars>"
dotnet user-secrets set "ENCRYPTION_KEY" "<64 hex chars>"
dotnet user-secrets set "PUBLIC_URL" "https://<your-public-https-domain>"
dotnet user-secrets set "GOOGLE_CLIENT_ID" "..."
dotnet user-secrets set "GOOGLE_CLIENT_SECRET" "..."
dotnet user-secrets list      # check what is set
```

Generate a random 64-hex value: `openssl rand -hex 32`.

### Required at startup

`ConnectionStrings:DefaultConnection`, `JWT_ACCESS_SECRET` (>= 32 chars), `JWT_REFRESH_SECRET` (>= 32),
`ENCRYPTION_KEY` (>= 32), `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `PUBLIC_URL`.

### Optional integrations

Instagram (`INSTAGRAM_*`), WhatsApp (`WHATSAPP_*`), Microsoft (`MICROSOFT_*`), Gemini (`GEMINI_API_KEY`),
S3 (`AWS_*`), SMTP (`EmailSettings:*`). The full list with comments is in
`apps/api/WebApi/user-secrets.example.json` and `.env.example`.

### Production

Set the same keys as environment variables. Nested keys use a double underscore:
`ConnectionStrings__DefaultConnection`, `EmailSettings__Credentials__Password`.
`ASPNETCORE_ENVIRONMENT=Production` selects S3 file storage instead of local disk.

### Warning: `ENCRYPTION_KEY`

It encrypts the stored Instagram/WhatsApp access tokens. Changing it makes existing tokens
undecryptable; affected accounts must reconnect.

## Run locally

One command (checks tooling, user-secrets, PostgreSQL and the dev certificate, frees a stale API on
the port, applies migrations, starts the API):

```powershell
.\scripts\dev.ps1                 # https://localhost:7129 and http://localhost:5129
.\scripts\dev.ps1 -LaunchProfile http   # http://localhost:5129 only
.\scripts\dev.ps1 -Watch          # hot reload
.\scripts\dev.ps1 -CheckOnly      # report problems, change nothing
```

The same actions are available as VS Code tasks (`Terminal > Run Task`). Swagger UI: `/docs`.

Manual equivalent:

```powershell
cd apps/api
dotnet ef database update --project Infrastructure --startup-project WebApi
dotnet run --project WebApi --launch-profile https
dotnet test Tests
```

### Public HTTPS tunnel (ngrok) for Meta / Google / Microsoft callbacks

Instagram, WhatsApp and the OAuth providers need a public HTTPS URL. Use a static ngrok domain so
the URL never changes:

1. Install ngrok and claim a free static domain at <https://dashboard.ngrok.com/domains>.
2. Copy `docs/ngrok.example.yml` to your ngrok config (Windows: `C:\ngrok\ngrok.yml`) and fill in the
   authtoken and domain. Never commit the real file.
3. Start it: `ngrok start api`, or install it as a Windows service from an elevated shell:
   `ngrok service install --config C:\ngrok\ngrok.yml`, then `ngrok service start`.
4. `dotnet user-secrets set "PUBLIC_URL" "https://<your-static-domain>.ngrok-free.dev"` (no trailing slash).
5. Check: `https://<your-static-domain>.ngrok-free.dev/health/live` returns healthy.

### HTTPS in development

Run once to create and trust the ASP.NET Core development certificate: `dotnet dev-certs https --trust`.

The frontend keeps working against `http://localhost:5129`. If you point it at the HTTPS URL instead,
note that `http://localhost:5173` -> `https://localhost:7129` counts as cross-site for cookies, so the
refresh-token cookie needs `COOKIE_SECURE=true` and `COOKIE_SAMESITE=None`, or run the frontend over
HTTPS too. `CORS_ORIGIN` lists *frontend* origins (comma separated); in Development
`https://localhost:5173` and `127.0.0.1` variants are allowed automatically.

### Health checks

| URL | Meaning |
|---|---|
| `GET /health/live` | process is up (no dependencies) |
| `GET /health` | ready to serve: the database answers; `503` otherwise |

### Background jobs

Recurring jobs share `PeriodicBackgroundService`: one DI scope per tick, errors are logged and the next
tick still runs, and host shutdown exits quietly. `BackgroundServiceExceptionBehavior` is `Ignore`, so
a failing job can never take the API down.
