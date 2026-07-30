# IMPLEMENTATION_PLAN.md — Нақшаи иҷрои қадам ба қадам

> **Қоида барои Claude Code:** ин файлро аз боло то поён пайравӣ кун. Дар як
> вақт танҳо **ЯК ФАЗА**-ро сохта, тест кун, интизори тасдиқи корбар шав,
> баъд ба фазаи навбатӣ гузар. Ҳеҷ гоҳ ду фазаро якҷоя насоз.
>
> Ҳар фаза дорад: **Endpoint-ҳо** (чӣ сохта мешавад), **Вобастагӣ** (кадом
> фазаҳо бояд пеш аз ин тайёр бошанд), ва **Санҷиш** (чӣ тавр фаҳмем, ки
> фаза дуруст кор мекунад — пеш аз рафтан ба навбатӣ).

---

## Фазаи 0 — Заминаи проект

**Кор:**
- NestJS проекти нав (ё модул илова ба проекти мавҷуда)
- `prisma/schema.prisma` (файли дар ҳамин папка мавҷуда) → `npx prisma migrate dev`
- Пайвасти PostgreSQL ва Redis (Docker Compose барои local dev)
- `.env` бо ҳамаи variable-ҳо (нигаред ба ARCHITECTURE.md §9)
- `main.ts`, `app.module.ts`, global ValidationPipe, CORS, Helmet

**Вобастагӣ:** ҳеҷ

**Санҷиш:**
- [ ] `npm run start:dev` бе хатогӣ кор мекунад
- [ ] `npx prisma studio` DB-ро нишон медиҳад (ҳамаи ҷадвалҳо холӣ, аммо мавҷуданд)
- [ ] `GET /health` (агар илова карда бошӣ) 200 бармегардонад

---

## Фаза 1 — Auth + Companies + Users (заминаи ҳама чиз)

**Endpoint-ҳо:**
```
POST   /auth/register     # company + owner user якҷоя сохта мешавад
POST   /auth/login
POST   /auth/refresh
POST   /auth/logout
GET    /users
GET    /users/:id
POST   /users/invite
PATCH  /users/:id
PATCH  /users/:id/role
DELETE /users/:id
```

**Вобастагӣ:** Фазаи 0

**Санҷиш (ҳатмист пеш аз идома):**
- [ ] `/auth/register` → корбари нав + company нав дар DB пайдо мешавад, password hash шудааст (Argon2), на plaintext
- [ ] `/auth/login` бо маълумоти дуруст → access + refresh token бармегардонад
- [ ] `/auth/login` бо парол хато → 401, на 500
- [ ] Endpoint-и муҳофизатшуда (масалан `/users`) бе token → 401
- [ ] Бо token → 200, ва танҳо корбарони ҳамон company бармегарданд (санҷиш бо 2 company-и гуногун!)
- [ ] `/users/:id/role` — танҳо OWNER/ADMIN метавонад иваз кунад, EMPLOYEE кӯшиш кунад → 403

---

## Фаза 2 — Contacts (CRM)

**Endpoint-ҳо:**
```
GET    /contacts?search=&tag=&ownerId=&page=&limit=
GET    /contacts/:id
POST   /contacts
PATCH  /contacts/:id
DELETE /contacts/:id
GET    /contacts/:id/timeline
```

**Вобастагӣ:** Фазаи 1 (auth лозим барои ownerId, companyId)

**Санҷиш:**
- [ ] POST бо маълумоти дуруст → 201, дар DB companyId дуруст сабт шудааст
- [ ] GET рӯйхат → танҳо contact-ҳои ҳамон company (санҷиш бо 2 company!)
- [ ] Validation: email/phone-и нодуруст → 422 бо паёми равшан
- [ ] DELETE → contact нест мешавад, аммо deal/task-ҳои марбут чӣ мешаванд (риоя ба onDelete rule дар schema)
- [ ] `/contacts/:id/timeline` — ҳоло холӣ бармегардонад (Communication ҳанӯз нест) — ин дуруст аст, дар Фаза 6 пур мешавад

---

## Фаза 3 — Deals + Pipeline

**Endpoint-ҳо:**
```
GET    /deals?stage=&ownerId=
GET    /deals/:id
POST   /deals
PATCH  /deals/:id
PATCH  /deals/:id/stage
DELETE /deals/:id
```

**Вобастагӣ:** Фазаи 2 (Deal ба Contact пайваст аст)

**Санҷиш:**
- [ ] POST бо `contactId`-и вуҷуднадошта → 404/422, на 500
- [ ] `PATCH /deals/:id/stage` — stage иваз мешавад, аммо якҷоя бо probability мантиқан мувофиқат мекунад (масалан WON = 100%)?
- [ ] GET бо filter `?stage=WON` — фақат deal-ҳои дуруст бармегарданд

---

## Фаза 4 — Tasks (CRUD)

**Endpoint-ҳо:**
```
GET    /tasks?status=&assignedTo=&dealId=&contactId=
GET    /tasks/:id
POST   /tasks
PATCH  /tasks/:id
PATCH  /tasks/:id/status
DELETE /tasks/:id
```

**Вобастагӣ:** Фазаи 1 (assignedTo → User), Фазаи 2/3 (ихтиёрӣ: dealId/contactId)

**Санҷиш:**
- [ ] Task бе `dealId`/`contactId` сохта мешавад (ҳарду ихтиёранд)
- [ ] `assignedTo`-и корбари company-и дигар → рад карда шавад (баррасии multi-tenancy)
- [ ] `PATCH /tasks/:id/status` → COMPLETED — ин ба Time Tracking (Фазаи 5) таъсир намекунад, алоҳида кор мекунанд

---

## Фаза 5 — Time Tracking (Start / Stop)

**Endpoint-ҳо:**
```
POST   /tasks/:id/time/start
POST   /tasks/:id/time/stop
GET    /tasks/:id/time
GET    /tasks/time/active
DELETE /tasks/:id/time/:entryId
```

**Вобастагӣ:** Фазаи 4 (Task бояд мавҷуд бошад)

**Мантиқи бизнес** (нигаред ба ARCHITECTURE.md §4.4):
- Дар як вақт як корбар танҳо як timer running дошта метавонад (across ҳамаи task-ҳо)
- `durationSec` ҳатман дар backend ҳисоб карда мешавад

**Санҷиш:**
- [ ] `start` → TimeEntry сохта мешавад, `stoppedAt: null`
- [ ] Дубора `start` (бе stop) → 409 Conflict, на дубора entry
- [ ] `stop` → `durationSec` дуруст ҳисоб шудааст (санҷиш бо соат: 5 дақиқа интизор шав, фарқро бо DB муқоиса кун)
- [ ] `GET /tasks/time/active` — агар корбар refresh кунад, running timer-ро нишон медиҳад
- [ ] Task.status аз TODO → IN_PROGRESS автомат иваз мешавад ҳангоми start (тибқи қоида)

---

## Фаза 6 — Communications (Manual Notes + Unified Model)

**Endpoint-ҳо:**
```
GET    /communications?contactId=&channel=
POST   /communications/notes
```

**Вобастагӣ:** Фазаи 2 (Contact)

**Санҷиш:**
- [ ] Note нав → дар `/contacts/:id/timeline` (Фазаи 2) пайдо мешавад
- [ ] `channel: MANUAL_NOTE` дуруст сабт мешавад

---

## Фаза 7 — WhatsApp Integration

**Зина 7.1 — Webhook + гирифтани паём (receive only)**
```
POST/GET /webhooks/whatsapp    # Meta verification + incoming
```
- [ ] Meta verification challenge (`GET` бо `hub.challenge`) дуруст ҷавоб медиҳад
- [ ] Signature verification (HMAC) — паёми бе имзои дуруст → 401
- [ ] Паёми воридшуда дар DB ҳамчун Communication сабт мешавад, Contact бо phone мувофиқат мекунад (ё нав сохта мешавад)
- [ ] Дубора фиристодани ҳамон webhook payload (duplicate) → дубора сабт намешавад (idempotency)

**Зина 7.2 — Фиристодани паём**
```
POST /whatsapp/send
```
- [ ] Паём воқеан ба WhatsApp мерасад (бо test number санҷед)
- [ ] Хатогии Meta API (масалан token нодуруст) → 400/502 бо паёми равшан, на crash

**Зина 7.3 — Real-time (WebSocket)**
- [ ] Паёми нав → frontend бе refresh нишон медиҳад (Socket.IO event)

**Вобастагӣ:** Фазаи 6 (Communication model)

---

## Фаза 8 — Email Integration (Gmail/Outlook)

**Зина 8.1 — OAuth connect**
```
GET /email/connect/gmail
GET /email/connect/gmail/callback
GET /email/connect/outlook
GET /email/connect/outlook/callback
```
- [ ] Пас аз OAuth, `OAuthConnection` бо token-ҳои **encrypted** сабт мешавад (санҷед дар DB — набояд plaintext бошад)

**Зина 8.2 — Webhook/sync**
```
POST /webhooks/email/gmail
POST /webhooks/email/outlook
```
- [ ] Номаи нав → Communication сабт мешавад

**Зина 8.3 — Фиристодан**
```
POST /email/send
```

**Вобастагӣ:** Фазаи 6

---

## Фаза 9 — AI Pipeline

**Endpoint-ҳо:**
```
POST /ai/summarize/:communicationId
GET  /ai/summary/:communicationId
POST /ai/query
POST /ai/draft-reply
```

**Вобастагӣ:** Фазаи 6, 7 (ба Communication ниёз дорад)

**Санҷиш:**
- [ ] `summarize` бо як сӯҳбати воқеӣ → JSON бо `summary/sentiment/actionItems` дуруст бармегардад
- [ ] Debounce (30s) — агар 3 паём паиҳам оянд, танҳо ЯК summary сохта мешавад, на 3
- [ ] `ai/query` бо "мизоҷоне, ки 14 рӯз ҷавоб надодаанд" → рӯйхати дурусти contact бармегардад

---

## Фаза 10 — Analytics Dashboard

**Endpoint-ҳо:**
```
GET /dashboard
GET /dashboard/sales
GET /dashboard/revenue
GET /dashboard/tasks
GET /dashboard/activity-feed
```

**Вобастагӣ:** Ҳамаи фазаҳои қаблӣ (aggregate маълумот аз ҳама)

**Санҷиш:**
- [ ] Рақамҳо бо маълумоти воқеии DB (санҷед бо SQL query дастӣ, муқоиса кунед)
- [ ] Company-и дигар маълумоти дигар мебинад (multi-tenancy санҷиш охирин бор)

---

## Фаза 11 — Security Hardening + Audit + Deployment

- [ ] Rate limiting фаъол (санҷед: 100+ дархост дар як дақиқа → 429)
- [ ] Audit log дар ҳар delete/role-change сабт мешавад
- [ ] Docker build бе хатогӣ
- [ ] CI/CD (GitHub Actions) — тест автомат иҷро мешавад

---

## Тартиби умумии кор бо Claude Code

1. Ин файлро пурра бихон
2. Бигӯ кадом фаза ҳастӣ (масалан "Ман ҳозир Фазаи 1-ро сар мекунам")
3. Танҳо endpoint-ҳои ҳамон фазаро бисоз
4. Чек-листи "Санҷиш"-и ҳамон фазаро пешниҳод кун — аз ман хоҳиш кун ки худам санҷам ё бо тест-и худкор тасдиқ кун
5. Танҳо пас аз тасдиқи ман → ба фазаи навбатӣ гузар
6. Агар фаза fail шавад — дар ҳамон фаза бимон, ислоҳ кун, дубора санҷ
