# Architecture

Business OS is a multi-tenant B2B CRM API for Central Asia: CRM, tasks and time tracking, a unified
inbox (WhatsApp, Telegram, Instagram, email), AI helpers, invoicing/billing, workflows and reporting.

This document replaces the original NestJS/Prisma design notes (removed from the repo; still in git
history). Only ideas that remain valid for the .NET backend are kept. Where a statement was carried
over from the old design and not re-verified against the code, it is marked *(design intent)*.

## Solution layout (`apps/api`)

| Project | Role |
|---|---|
| `Domain` | Entities (grouped by area under `Entities/`), enums, small interfaces such as `ITenantScoped`. No dependencies. |
| `Application` | Services (`*Service`), their interfaces, DTOs and FluentValidation validators (`ViewModels/`), abstractions for anything external (`Abstraction/`: `IMailService`, `IInstagramApiClient`, `ITokenCipher`, ...). |
| `Infrastructure` | EF Core `DataContext` and migrations, API clients (Meta, Telegram, Instagram, Gmail, Outlook, Gemini), SMTP, S3/local file storage, SignalR, JWT/Argon2/AES-GCM, hosted background services. |
| `WebApi` | Controllers, `Program.cs`, middleware (exceptions, tenant context), permission filter. |
| `Tests` | xUnit unit tests (SQLite in-memory for data access). |

Dependencies point inward: WebApi -> Application/Infrastructure -> Domain. Application never
references Infrastructure; Infrastructure implements the Application abstractions and is wired in
`Infrastructure/DependencyInjection.cs`.

## Multi-tenancy

- Every tenant-owned entity implements `ITenantScoped` (`CompanyId`). `TenantContextMiddleware` fills
  `ICurrentTenantContext` from the JWT; EF query filters and `TenantSaveChangesInterceptor` apply the
  tenant automatically.
- Services still take `companyId` explicitly for lookups by id, so a wrong id returns "not found"
  instead of another tenant's row.
- Contacts can additionally be branch-scoped (`IBranchScoped`, `IContactBranchScoped`).
- *(design intent)* PostgreSQL Row-Level Security as a second layer is not implemented.

## Authentication and authorization

- JWT access token (short-lived) with `sub`, `companyId`, role claims; refresh token in an httpOnly
  cookie (`COOKIE_SECURE`, `COOKIE_SAMESITE` configurable). Passwords use Argon2.
- RBAC: roles and permissions in the database; `PermissionAuthorizationFilter` checks them per
  action, `[RequireModule(...)]` gates endpoints on the company's subscription plan.
- Fallback authorization policy requires an authenticated user; public endpoints (webhooks, OAuth
  callbacks, health) opt out with `[AllowAnonymous]`.
- Secrets that must be recoverable (OAuth/API tokens) are encrypted with AES-256-GCM
  (`AesGcmTokenCipher`, key derived from `ENCRYPTION_KEY`).

## Integrations and webhooks

| Endpoint | Verification |
|---|---|
| `GET/POST /webhooks/whatsapp` | `hub.verify_token` + `X-Hub-Signature-256` (HMAC-SHA256 of the raw body with the app secret) |
| `GET/POST /webhooks/instagram` | same scheme with the Instagram app secret |
| `POST /webhooks/telegram/{secret}` | secret in path and `X-Telegram-Bot-Api-Secret-Token` header |
| `POST /webhooks/google-calendar` | `X-Goog-Channel-Id` / `X-Goog-Channel-Token` |
| `POST /webhooks/payments/{provider}` | delegated to `IPaymentProvider` |

Inbound message flow (WhatsApp/Instagram/Telegram), a design that still holds:

1. Verify the signature against the **raw** request body.
2. Return `200` immediately (Meta times out after ~20 s) and enqueue the work on
   `IBackgroundTaskQueue`; `QueuedHostedService` runs it in its own DI scope.
3. Idempotency: skip messages whose `ProviderMessageId` was already stored.
4. Match or create the contact, save a `Communication`, push a SignalR event
   (`/hubs/messaging`) and create a notification for the contact owner.

Public base URL: `PUBLIC_URL` is the single source for every OAuth redirect URI and webhook URL
(Google, Microsoft, Instagram). Meta requires public HTTPS, so local development needs a tunnel
with a stable domain.

Instagram uses "Instagram API with Instagram Login": the user authorizes on Instagram's own page,
the callback exchanges code -> short-lived -> long-lived (~60 day) token, which a daily hosted
service refreshes before expiry.

## Background jobs (`Infrastructure/BackgroundJobs`)

`QueuedHostedService` (work queue), `WorkflowSchedulerHostedService` (1 min),
`TaskDueNotificationHostedService` (15 min), `SubscriptionInvoicingHostedService` (1 h),
`InstagramTokenRefreshHostedService` and `WhatsAppTokenRefreshHostedService` (daily). Each tick
creates its own DI scope and catches its own errors.

## AI

`AiService` calls Google Gemini (`GeminiClient`) for quotation generation, natural-language queries
over CRM data and summaries; `AiUsageService` records usage per tenant. The original design's
flow still describes the intent: build context from recent communications, request structured JSON,
persist the result, notify the frontend.

## Time tracking rules (*design intent*, implemented in `TimeEntriesService`)

- One running timer per user across all tasks.
- Durations are computed on the server, never accepted from the client.
- A "currently running" endpoint lets the UI restore timer state after a refresh.

## Security checklist

| Item | Status |
|---|---|
| Argon2 password hashing | done |
| AES-256-GCM for stored tokens | done (key from configuration, no KMS) |
| Webhook signature verification | done for Meta, Telegram, Google |
| CORS restricted to configured origins | done (`CORS_ORIGIN`) |
| Rate limiting | `auth` policy only |
| Audit log of sensitive actions | done (`AuditLogService`) |
| Input validation | FluentValidation per request DTO |
| TLS | terminate at the reverse proxy / tunnel |
| Secrets out of the repository | done (user-secrets / environment variables) |

## Configuration

See `README.md` and `apps/api/WebApi/user-secrets.example.json`.
