# Unified Business OS — Полная Backend Архитектура (MVP)

> Ин ҳуҷҷат — версияи пурраи техникӣ барои backend, ки метавонед мустақим ҳамчун **master prompt барои Claude Code** истифода баред (масалан: "Ин архитектураро implement кун дар NestJS project-и ман").

**Stack:** NestJS + TypeScript + PostgreSQL + Prisma + Redis + BullMQ + S3

---

## 1. Модул-модул сохтори Backend (NestJS)

```
apps/api/src/
├── main.ts
├── app.module.ts
│
├── common/
│   ├── decorators/          # @CurrentUser(), @Roles(), @CompanyId()
│   ├── guards/              # JwtAuthGuard, RolesGuard, CompanyGuard
│   ├── interceptors/        # LoggingInterceptor, TransformInterceptor
│   ├── filters/             # AllExceptionsFilter
│   ├── pipes/               # ValidationPipe config
│   └── middleware/          # RawBodyMiddleware (webhook signature verification)
│
├── config/
│   ├── configuration.ts
│   ├── database.config.ts
│   ├── redis.config.ts
│   └── validation.schema.ts # Joi/Zod env validation
│
├── prisma/
│   ├── prisma.module.ts
│   ├── prisma.service.ts
│   └── schema.prisma
│
├── modules/
│   ├── auth/
│   │   ├── auth.module.ts
│   │   ├── auth.controller.ts
│   │   ├── auth.service.ts
│   │   ├── strategies/       # JwtStrategy, GoogleStrategy, MicrosoftStrategy
│   │   └── dto/
│   │
│   ├── companies/            # Multi-tenancy root
│   │   ├── companies.module.ts
│   │   ├── companies.controller.ts
│   │   ├── companies.service.ts
│   │   └── dto/
│   │
│   ├── users/
│   │   ├── users.module.ts
│   │   ├── users.controller.ts
│   │   ├── users.service.ts
│   │   └── dto/
│   │
│   ├── contacts/              # CRM
│   │   ├── contacts.module.ts
│   │   ├── contacts.controller.ts
│   │   ├── contacts.service.ts
│   │   └── dto/
│   │
│   ├── deals/                 # Pipeline
│   │   ├── deals.module.ts
│   │   ├── deals.controller.ts
│   │   ├── deals.service.ts
│   │   └── dto/
│   │
│   ├── tasks/
│   │   ├── tasks.module.ts
│   │   ├── tasks.controller.ts
│   │   ├── tasks.service.ts
│   │   └── dto/
│   │
│   ├── communications/        # Unified inbox (WhatsApp + Email + Notes)
│   │   ├── communications.module.ts
│   │   ├── communications.controller.ts
│   │   ├── communications.service.ts
│   │   └── dto/
│   │
│   ├── whatsapp/
│   │   ├── whatsapp.module.ts
│   │   ├── whatsapp.controller.ts   # webhook endpoint
│   │   ├── whatsapp.service.ts      # send/receive logic
│   │   └── whatsapp.processor.ts    # BullMQ consumer
│   │
│   ├── email/
│   │   ├── email.module.ts
│   │   ├── email.controller.ts      # OAuth callback + webhook
│   │   ├── gmail.service.ts
│   │   ├── outlook.service.ts
│   │   └── email.processor.ts
│   │
│   ├── ai/
│   │   ├── ai.module.ts
│   │   ├── ai.controller.ts
│   │   ├── ai.service.ts            # Claude/OpenAI API calls
│   │   └── ai.processor.ts          # BullMQ summary job consumer
│   │
│   ├── notifications/
│   │   ├── notifications.module.ts
│   │   ├── notifications.gateway.ts # WebSocket (Socket.IO)
│   │   └── notifications.service.ts
│   │
│   ├── analytics/
│   │   ├── analytics.module.ts
│   │   ├── analytics.controller.ts
│   │   └── analytics.service.ts
│   │
│   └── integrations/
│       ├── integrations.module.ts
│       ├── integrations.controller.ts   # manage OAuth connections
│       └── integrations.service.ts
│
├── queue/
│   ├── queue.module.ts       # BullMQ registration for all queues
│   └── queue.constants.ts    # queue names, job names
│
└── storage/
    ├── storage.module.ts
    └── s3.service.ts
```

**Принсип:** ҳар модул = як domain, бо `Module / Controller / Service / DTO` алоҳида. Ин ба шумо имкон медиҳад дар оянда ҳар модулро ба microservice ҷудо кунед бе рефакторинги калон.

---

## 2. Prisma Schema — Пурра

```prisma
generator client {
  provider = "prisma-client-js"
}

datasource db {
  provider = "postgresql"
  url      = env("DATABASE_URL")
}

// ============ CORE ============

model Company {
  id        String   @id @default(uuid())
  name      String
  industry  String?
  plan      Plan     @default(STARTER)
  timezone  String   @default("Asia/Dushanbe")
  createdAt DateTime @default(now())
  updatedAt DateTime @updatedAt

  users            User[]
  contacts         Contact[]
  deals            Deal[]
  tasks            Task[]
  communications   Communication[]
  oauthConnections OAuthConnection[]
  waAccounts       WhatsAppAccount[]
  auditLogs        AuditLog[]

  @@map("companies")
}

enum Plan {
  STARTER
  PROFESSIONAL
  ENTERPRISE
}

model User {
  id           String    @id @default(uuid())
  companyId    String
  company      Company   @relation(fields: [companyId], references: [id], onDelete: Cascade)
  name         String
  email        String    @unique
  passwordHash String?
  role         Role      @default(EMPLOYEE)
  avatarUrl    String?
  status       UserStatus @default(ACTIVE)
  lastLoginAt  DateTime?
  createdAt    DateTime  @default(now())
  updatedAt    DateTime  @updatedAt

  ownedContacts    Contact[]      @relation("ContactOwner")
  ownedDeals       Deal[]         @relation("DealOwner")
  assignedTasks    Task[]         @relation("TaskAssignee")
  timeEntries      TimeEntry[]    @relation("TimeEntryUser")
  oauthConnections OAuthConnection[]
  auditLogs        AuditLog[]
  refreshTokens    RefreshToken[]

  @@index([companyId])
  @@map("users")
}

enum Role {
  OWNER
  ADMIN
  MANAGER
  SALES
  SUPPORT
  EMPLOYEE
  READ_ONLY
}

enum UserStatus {
  ACTIVE
  INVITED
  SUSPENDED
}

model RefreshToken {
  id        String   @id @default(uuid())
  userId    String
  user      User     @relation(fields: [userId], references: [id], onDelete: Cascade)
  tokenHash String
  expiresAt DateTime
  revoked   Boolean  @default(false)
  createdAt DateTime @default(now())

  @@index([userId])
  @@map("refresh_tokens")
}

// ============ CRM ============

model Contact {
  id        String   @id @default(uuid())
  companyId String
  company   Company  @relation(fields: [companyId], references: [id], onDelete: Cascade)
  firstName String
  lastName  String?
  email     String?
  phone     String?  // E.164, used to match WhatsApp sender
  position  String?
  companyName String?
  notes     String?
  ownerId   String?
  owner     User?    @relation("ContactOwner", fields: [ownerId], references: [id])
  tags      String[] @default([])
  createdAt DateTime @default(now())
  updatedAt DateTime @updatedAt

  deals          Deal[]
  tasks          Task[]
  communications Communication[]

  @@index([companyId])
  @@index([phone])
  @@index([email])
  @@map("contacts")
}

model Deal {
  id                 String    @id @default(uuid())
  companyId          String
  contactId          String
  contact            Contact   @relation(fields: [contactId], references: [id], onDelete: Cascade)
  title              String
  value              Decimal   @default(0)
  currency            String    @default("TJS")
  stage              DealStage @default(LEAD)
  probability        Int       @default(0) // 0-100
  expectedCloseDate  DateTime?
  ownerId            String?
  owner              User?     @relation("DealOwner", fields: [ownerId], references: [id])
  createdAt          DateTime  @default(now())
  updatedAt          DateTime  @updatedAt

  tasks Task[]

  @@index([companyId])
  @@index([stage])
  @@map("deals")
}

enum DealStage {
  LEAD
  QUALIFIED
  PROPOSAL
  NEGOTIATION
  WON
  LOST
}

model Task {
  id          String       @id @default(uuid())
  companyId   String
  title       String
  description String?
  priority    TaskPriority @default(MEDIUM)
  status      TaskStatus   @default(TODO)
  assignedTo  String?
  assignee    User?        @relation("TaskAssignee", fields: [assignedTo], references: [id])
  dueDate     DateTime?
  dealId      String?
  deal        Deal?        @relation(fields: [dealId], references: [id], onDelete: SetNull)
  contactId   String?
  contact     Contact?     @relation(fields: [contactId], references: [id], onDelete: SetNull)
  createdAt   DateTime     @default(now())
  updatedAt   DateTime     @updatedAt

  timeEntries TimeEntry[]

  @@index([companyId])
  @@index([assignedTo])
  @@map("tasks")
}

enum TaskPriority {
  LOW
  MEDIUM
  HIGH
  URGENT
}

enum TaskStatus {
  TODO
  IN_PROGRESS
  COMPLETED
  ARCHIVED
}

// ============ TIME TRACKING (Start / Stop) ============

model TimeEntry {
  id         String    @id @default(uuid())
  companyId  String
  taskId     String
  task       Task      @relation(fields: [taskId], references: [id], onDelete: Cascade)
  userId     String
  user       User      @relation("TimeEntryUser", fields: [userId], references: [id])
  startedAt  DateTime  @default(now())
  stoppedAt  DateTime?          // null = timer ҳоло идома дорад (running)
  durationSec Int?              // ҳисоб карда мешавад ҳангоми stop
  note       String?
  createdAt  DateTime  @default(now())

  @@index([taskId])
  @@index([userId])
  @@index([companyId])
  @@map("time_entries")
}

// ============ COMMUNICATIONS ============

model Communication {
  id                String       @id @default(uuid())
  companyId         String
  company           Company      @relation(fields: [companyId], references: [id], onDelete: Cascade)
  contactId         String?
  contact           Contact?     @relation(fields: [contactId], references: [id], onDelete: SetNull)
  channel           Channel
  direction         Direction
  content           String
  attachments       Json?        // [{url, type, name}]
  providerMessageId String?      @unique
  status            MessageStatus @default(RECEIVED)
  timestamp         DateTime     @default(now())

  aiSummary AiSummary?

  @@index([companyId, contactId])
  @@index([channel])
  @@map("communications")
}

enum Channel {
  WHATSAPP
  EMAIL
  MANUAL_NOTE
  PHONE_CALL
}

enum Direction {
  INBOUND
  OUTBOUND
}

enum MessageStatus {
  RECEIVED
  SENT
  FAILED
  QUEUED
}

model AiSummary {
  id              String    @id @default(uuid())
  communicationId String    @unique
  communication   Communication @relation(fields: [communicationId], references: [id], onDelete: Cascade)
  summary         String
  sentiment       Sentiment?
  actionItems     Json?     // string[]
  nextFollowUp    DateTime?
  generatedAt     DateTime  @default(now())

  @@map("ai_summaries")
}

enum Sentiment {
  POSITIVE
  NEUTRAL
  NEGATIVE
}

// ============ INTEGRATIONS ============

model OAuthConnection {
  id                   String   @id @default(uuid())
  companyId            String
  company              Company  @relation(fields: [companyId], references: [id], onDelete: Cascade)
  userId               String
  user                 User     @relation(fields: [userId], references: [id], onDelete: Cascade)
  provider             OAuthProvider
  encryptedAccessToken String
  encryptedRefreshToken String?
  scope                String?
  expiresAt            DateTime?
  createdAt            DateTime @default(now())
  updatedAt            DateTime @updatedAt

  @@unique([userId, provider])
  @@map("oauth_connections")
}

enum OAuthProvider {
  GOOGLE
  MICROSOFT
}

model WhatsAppAccount {
  id                String   @id @default(uuid())
  companyId         String
  company           Company  @relation(fields: [companyId], references: [id], onDelete: Cascade)
  phoneNumberId     String   @unique   // Meta WhatsApp phone_number_id
  wabaId            String              // WhatsApp Business Account ID
  encryptedAccessToken String
  verifiedName      String?
  createdAt         DateTime @default(now())

  @@map("whatsapp_accounts")
}

// ============ AUDIT ============

model AuditLog {
  id        String   @id @default(uuid())
  companyId String
  company   Company  @relation(fields: [companyId], references: [id], onDelete: Cascade)
  userId    String?
  user      User?    @relation(fields: [userId], references: [id], onDelete: SetNull)
  action    String   // "deal.deleted", "user.role_changed"
  entity    String
  entityId  String?
  metadata  Json?
  createdAt DateTime @default(now())

  @@index([companyId])
  @@map("audit_logs")
}
```

---

## 3. REST API — Ҳамаи endpoint-ҳо

### Auth
```
POST   /auth/register          # company + owner user
POST   /auth/login
POST   /auth/refresh
POST   /auth/logout
GET    /auth/google             # OAuth redirect
GET    /auth/google/callback
GET    /auth/microsoft
GET    /auth/microsoft/callback
```

### Users
```
GET    /users
GET    /users/:id
POST   /users/invite
PATCH  /users/:id
PATCH  /users/:id/role
DELETE /users/:id
```

### Contacts (CRM)
```
GET    /contacts?search=&tag=&ownerId=&page=&limit=
GET    /contacts/:id                 # includes: deals, tasks, communications, aiSummary (timeline view)
POST   /contacts
PATCH  /contacts/:id
DELETE /contacts/:id
GET    /contacts/:id/timeline        # merged WhatsApp + Email + notes, chronological
```

### Deals
```
GET    /deals?stage=&ownerId=
GET    /deals/:id
POST   /deals
PATCH  /deals/:id
PATCH  /deals/:id/stage              # kanban drag-drop
DELETE /deals/:id
```

### Tasks
```
GET    /tasks?status=&assignedTo=&dealId=&contactId=
GET    /tasks/:id
POST   /tasks
PATCH  /tasks/:id
PATCH  /tasks/:id/status
DELETE /tasks/:id

# Time Tracking (Start / Stop)
POST   /tasks/:id/time/start        # {note?}  → сохтани TimeEntry нав (stoppedAt: null)
POST   /tasks/:id/time/stop         # ёфтани running entry-и корбари ҷорӣ → stoppedAt=now, durationSec ҳисоб
GET    /tasks/:id/time              # ҳамаи TimeEntry-ҳои ин task (барои ҳисобот)
GET    /tasks/time/active           # timer-и ҳозир running-и корбари ҷорӣ (агар бошад) — барои frontend "resume UI"
DELETE /tasks/:id/time/:entryId     # ислоҳи хатогӣ (танҳо OWNER/ADMIN/MANAGER)
```

### Communications (Unified Inbox)
```
GET    /communications?contactId=&channel=
POST   /communications/notes         # manual note
```

### WhatsApp
```
POST   /webhooks/whatsapp            # Meta verification (GET) + incoming messages (POST)
POST   /whatsapp/send                # {contactId, message}
GET    /whatsapp/accounts
POST   /whatsapp/accounts            # connect a business number
```

### Email
```
GET    /email/connect/gmail
GET    /email/connect/gmail/callback
GET    /email/connect/outlook
GET    /email/connect/outlook/callback
POST   /webhooks/email/gmail         # Google Pub/Sub push notification
POST   /webhooks/email/outlook       # Microsoft Graph change notification
POST   /email/send
```

### AI
```
POST   /ai/summarize/:communicationId   # manual trigger
GET    /ai/summary/:communicationId
POST   /ai/query                        # natural-language query: "clients silent 14 days"
POST   /ai/draft-reply                  # {contactId, context} -> suggested reply
```

### Analytics
```
GET    /dashboard
GET    /dashboard/sales
GET    /dashboard/revenue
GET    /dashboard/tasks
GET    /dashboard/activity-feed
```

### Integrations
```
GET    /integrations                 # list connected services + status
DELETE /integrations/:id             # disconnect
```

---

## 4. Webhook Architecture (муҳимтарин қисм)

### 4.1 WhatsApp (Meta Cloud API)

```
Meta → POST /webhooks/whatsapp
   │
   ├─ Middleware: verify X-Hub-Signature-256 (HMAC SHA256, app secret)
   │       └─ raw body лозим аст (RawBodyMiddleware) — Prisma/NestJS JSON parser
   │          bodyRo tahrif namekunad
   │
   ├─ Controller: return 200 фавран (Meta 20 сония timeout дорад)
   │
   ├─ Push job → BullMQ queue "whatsapp-incoming"
   │
   └─ Processor:
        1. Idempotency check (providerMessageId already exists?)
        2. Match/create Contact by phone number
        3. Save Communication (channel: WHATSAPP, direction: INBOUND)
        4. Emit WebSocket event → frontend real-time update
        5. Enqueue "ai-summarize" job (debounced — wait 30s барои чанд паём якҷоя шаванд)
```

### 4.2 Email (Gmail / Outlook)

```
Gmail: OAuth grant → Google Pub/Sub watch() on mailbox
   → Push notification → POST /webhooks/email/gmail
   → historyId дар notification → Gmail API history.list() → фаҳмидани паёми нав
   → Save Communication

Outlook: Microsoft Graph subscription (webhook) — expires ҳар 3 рӯз, renew кардан лозим
   (BullMQ cron job: "renew-graph-subscriptions" ҳар 24 соат)
```

**Муҳим:** ҳар ду provider token refresh лозим доранд — encrypted refresh token-ро истифода бурда, як `TokenRefreshService` соатӣ (BullMQ repeatable job) токенҳои қарибулмуҳлатро renew мекунад.

### 4.3 Идемпотентнокӣ ва Retry

- Ҳар webhook event → `providerMessageId` уникалӣ дар DB (unique constraint), duplicate = silent skip.
- BullMQ job-ҳо: `attempts: 5`, `backoff: exponential`.
- Dead-letter queue барои job-ҳое, ки 5 бор fail шуданд → alert ба admin.

---

## 4.4 Мантиқи Start / Stop (Time Tracking)

```
POST /tasks/:id/time/start
   1. Санҷиш: оё ин корбар аллакай як TimeEntry running дорад (stoppedAt = null)?
      → Агар бале: 409 Conflict "Аввал timer-и қаблиро stop кун"
      (ё, ба ихтиёри маҳсулот: automatic stop карда, нав start мекунад)
   2. Сохтани TimeEntry: { taskId, userId, startedAt: now(), stoppedAt: null }
   3. Task.status → IN_PROGRESS (агар TODO буд)
   4. WebSocket event "timer-started" → frontend (дигар корбарони ҳамон company бинанд, ки кӣ кор карда истодааст)

POST /tasks/:id/time/stop
   1. Ёфтани running TimeEntry-и корбари ҷорӣ барои ин task
   2. Агар нест → 404 "Timer running нест"
   3. stoppedAt = now(); durationSec = stoppedAt - startedAt
   4. WebSocket event "timer-stopped"
   5. Ҷамъбасти умумии вақти сарфшуда ба task (SUM(durationSec)) дар response бармегардад

GET /tasks/time/active
   → барои он ки корбар агар саҳифаро refresh кунад ё аз дастгоҳи дигар ворид шавад,
     frontend фаҳмад, ки кадом task ҳоло "running" аст ва UI-ро дуруст нишон диҳад
```

**Қоидаҳо:**
- Дар як вақт як корбар танҳо як timer running дошта метавонад (across all tasks) — ин соддатарин ва аз хатогӣ бехатартарин рафтор.
- `durationSec` ҳатман дар backend ҳисоб карда мешавад (на аз frontend қабул), то манипулятсия пешгирӣ шавад.
- Ҳисоботи умумии вақт дар як task: `GET /tasks/:id` метавонад `totalTimeSec` (aggregate) низ баргардонад.
- Барои dashboard: `GET /dashboard` метавонад "Соатҳои кории имрӯза" widget-ро низ дар бар гирад (SUM аз TimeEntry-ҳои имрӯза).

---

## 5. BullMQ Queue Тарҳ

```
queue.constants.ts:

QUEUES = {
  WHATSAPP_INCOMING: 'whatsapp-incoming',
  EMAIL_INCOMING:    'email-incoming',
  AI_SUMMARIZE:      'ai-summarize',
  TOKEN_REFRESH:     'token-refresh',
  NOTIFICATIONS:     'notifications',
}
```

Ҳар queue = алоҳида Processor class бо `@Processor(QUEUES.X)` decorator, concurrency-и худ (масалан AI queue concurrency: 3, чунки Claude API rate limit дорад).

---

## 6. AI Pipeline (муфассал)

```
Trigger: нав Communication сохта шуд
   ↓
Debounce 30s (агар паёми дигар омад, тоймерро reset кун)
   ↓
Job: ai-summarize
   ↓
1. Гирифтани охирин N паёми ин Contact (context window)
2. Сохтани prompt:

   System: "Ту ёрдамчии CRM ҳастӣ. Сӯҳбатро таҳлил кун ва бо JSON ҷавоб деҳ."
   User: <охирин N паём>
   
   Format (structured output):
   {
     "summary": string,
     "sentiment": "POSITIVE"|"NEUTRAL"|"NEGATIVE",
     "action_items": string[],
     "next_follow_up": ISO date | null
   }

3. Call Claude API (model: claude-sonnet-4-6, response_format: JSON)
4. Parse → save AiSummary
5. Emit WebSocket "ai-summary-ready" → frontend
```

`/ai/query` барои табиии-забон query-ҳо (масалан "мизоҷоне, ки 14 рӯз ҷавоб надодаанд"):
```
1. User query → Claude бо tool-calling: "search_contacts" tool
2. Claude tool-ро дархост мекунад бо parameters (masalan: {noReplyDays: 14})
3. Backend ин query-ро ба Prisma табдил медиҳад ва натиҷаро ба Claude бармегардонад
4. Claude ҷавоби ниҳоиро ба забони табиӣ месозад
```

---

## 7. Auth, RBAC ва Multi-tenancy

### JWT Strategy
- Access token: 15 daqiqa, payload: `{ sub: userId, companyId, role }`
- Refresh token: 30 рӯз, дар `httpOnly` cookie, hash дар DB нигоҳ дошта мешавад (на raw token)

### CompanyGuard (муҳимтарин барои multi-tenancy)
```typescript
// Ҳар query дар service-ҳо ҳатман бо companyId filter мешавад
// Мисол:
async findAll(companyId: string) {
  return this.prisma.contact.findMany({ where: { companyId } });
}
```
Илова бар ин, тавсия: PostgreSQL **Row-Level Security (RLS)** барои қабати иловагии ҳимоя — агар кӯдак хатогии кодӣ рӯй диҳад (масалан companyId фаромӯш шуд), RLS сатҳи DB боз ҳам муҳофизат мекунад.

### RolesGuard
```typescript
@Roles(Role.ADMIN, Role.OWNER)
@UseGuards(JwtAuthGuard, RolesGuard, CompanyGuard)
@Delete(':id')
deleteContact(...) {}
```

---

## 8. Security чеклист

- [ ] Passwords: Argon2id
- [ ] OAuth/WhatsApp tokens: AES-256-GCM encryption, key дар KMS/Vault, на дар .env
- [ ] TLS 1.3 ҳатмӣ дар production (Cloudflare/nginx)
- [ ] Rate limiting: `@nestjs/throttler` — 100 req/min per IP, 20 req/min per webhook endpoint
- [ ] Webhook signature verification (Meta HMAC, Google/Microsoft JWT validation)
- [ ] CORS: танҳо frontend domain
- [ ] Helmet middleware
- [ ] SQL injection: Prisma parametrized queries (built-in)
- [ ] Audit log: ҳар delete/role-change/export action
- [ ] Input validation: Zod DTO дар ҳар controller

---

## 9. Environment Variables

```env
DATABASE_URL=postgresql://...
REDIS_URL=redis://...

JWT_ACCESS_SECRET=
JWT_REFRESH_SECRET=

GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
MICROSOFT_CLIENT_ID=
MICROSOFT_CLIENT_SECRET=

WHATSAPP_APP_SECRET=
WHATSAPP_VERIFY_TOKEN=
WHATSAPP_ACCESS_TOKEN=

ANTHROPIC_API_KEY=

AWS_S3_BUCKET=
AWS_ACCESS_KEY_ID=
AWS_SECRET_ACCESS_KEY=

ENCRYPTION_KEY=          # AES-256 key барои OAuth tokens
```

---

## 10. Roadmap иҷрои Backend (бо тартиб барои Claude Code)

1. **Setup**: NestJS monorepo, Prisma schema, PostgreSQL, Redis, Docker Compose (local dev)
2. **Auth + Companies + Users**: register/login/JWT/RBAC/CompanyGuard
3. **CRM core**: Contacts + Deals + Tasks CRUD
4. **Communications**: unified model + manual notes
5. **WhatsApp**: webhook + signature verification + send/receive + BullMQ processor
6. **Email**: Google/Microsoft OAuth + Pub/Sub webhook + BullMQ processor
7. **AI pipeline**: summarize job + Claude API integration + WebSocket notification
8. **Analytics**: dashboard aggregation queries
9. **Security hardening + audit logs + rate limiting**
10. **Deployment**: Docker → Render/Railway, CI/CD GitHub Actions

---

### Тавсия барои шумо

Барои Claude Code, беҳтараш ҳар қисматро (масалан "Auth + Multi-tenancy" ё "WhatsApp webhook module") алоҳида prompt кунед — на ҳамаашро якбора, чунки ин файл хеле калон аст ва натиҷаи беҳтарин вақте ба даст меояд, ки як модул пурра ва тест карда шавад, пеш аз рафтан ба навбатӣ.
