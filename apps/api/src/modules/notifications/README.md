# Real-time Notifications (Socket.IO)

Backend барои real-time update-ҳо аз Socket.IO истифода мебарад (`@nestjs/websockets` +
`@nestjs/platform-socket.io`). Ҳамаи логика дар се файл:

- `notifications.gateway.ts` — WebSocket gateway, JWT auth дар handshake, room-и `company:{companyId}`
- `notifications.service.ts` — wrapper бо як методи `emitToCompany(companyId, event, payload)`, дигар
  module-ҳо (whatsapp, email, tasks, deals, ai) ҳамин service-ро inject мекунанд, на gateway-ро мустақим
- `notifications.constants.ts` — номи event-ҳо (`NOTIFICATION_EVENTS`)

## Пайвастшавӣ аз клиент (Next.js)

Server-и Socket.IO дар ҳамон порт/host-и REST API кор мекунад (default path `/socket.io/`), CORS аз
`CORS_ORIGIN` идора мешавад. Ҳар socket бояд access token-и JWT-ро дар `auth.token` фиристад — сервер
онро verify мекунад (`jwt.accessSecret`), `sub`/`companyId`/`role`-ро мехонад ва socket-ро ба room-и
`company:{companyId}` ҳамроҳ мекунад. Агар token нодуруст ё вуҷуд надошта бошад, socket фавран
disconnect карда мешавад.

```ts
import { io } from 'socket.io-client';

const socket = io(process.env.NEXT_PUBLIC_API_URL, {
  auth: { token: accessToken }, // ҳамон JWT access token, ки бо REST API истифода мешавад
});

socket.on('connect', () => console.log('connected'));
socket.on('disconnect', () => console.log('disconnected'));
```

Ҳамаи event-ҳо танҳо ба корбарони ҳамон `companyId` мерасанд (tenant-и дигар ҳеҷ гоҳ event-ро
намебинад) — аз ин рӯ дар клиент лозим нест `companyId`-ро filter кунед, лекин ҳар payload барои
дурустӣ он `companyId`-ро дар бар мегирад (ба ғайр аз `task.assigned`, ки бевосита `companyId` дар task
намедорад — Task model-и companyId дорад, пас он ҳам ҳаст).

## Event-ҳо

| Event | Кай мебарояд | Аз куҷо |
|---|---|---|
| `whatsapp.message.new` | Паёми нави WhatsApp inbound қабул ва ҳамчун `Communication` захира шуд | `whatsapp.processor.ts` (BullMQ processor, пас аз webhook) |
| `email.new` | Email нав фиристода шуд ва ҳамчун `Communication` (channel `EMAIL`) захира шуд | `gmail.service.ts` / `outlook.service.ts` `.send()` |
| `task.assigned` | Task сохта шуд бо `assignedTo` ё `assignedTo`-и task тағйир ёфт | `tasks.service.ts` `create()` / `update()` |
| `ai.summary.ready` | Claude таҳлили сӯҳбатро тамом кард ва `AiSummary` захира шуд | `ai-summary.processor.ts` (BullMQ processor) |
| `deal.won` | Deal-и мавҷуда ба stage-и `WON` гузашт (танҳо дар лаҳзаи transition, на ҳар PATCH) | `deals.service.ts` `updateStage()` |

**Диққат:** `email.new` айни ҳол танҳо барои email-ҳои **фиристодашуда** (outbound, тавассути
`POST /email/send`) мебарояд, чунки inbound email sync/webhook (Gmail Pub/Sub watch, Microsoft Graph
subscription — нақшаи `ARCHITECTURE.md` §4.2) ҳанӯз sohta нашудааст. Вақте ки он handler сохта шавад,
бояд ҳамон `notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.EMAIL_NEW, communication)`-ро
дар он ҷо ҳам зангзанӣ кунад.

### Payload-ҳо

```ts
// whatsapp.message.new — пурраи Communication record
interface WhatsappMessageNewPayload {
  id: string;
  companyId: string;
  contactId: string | null;
  channel: 'WHATSAPP';
  direction: 'INBOUND' | 'OUTBOUND';
  content: string;
  attachments: unknown | null;
  providerMessageId: string | null;
  status: 'RECEIVED' | 'SENT' | 'FAILED' | 'QUEUED';
  timestamp: string; // ISO date
}

// email.new — пурраи Communication record (channel = 'EMAIL')
interface EmailNewPayload {
  id: string;
  companyId: string;
  contactId: string | null;
  channel: 'EMAIL';
  direction: 'INBOUND' | 'OUTBOUND';
  content: string;
  providerMessageId: string | null;
  status: 'RECEIVED' | 'SENT' | 'FAILED' | 'QUEUED';
  timestamp: string;
}

// task.assigned — пурраи Task record (бо assignee include шуда)
interface TaskAssignedPayload {
  id: string;
  companyId: string;
  title: string;
  description: string | null;
  priority: 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';
  status: 'TODO' | 'IN_PROGRESS' | 'COMPLETED' | 'ARCHIVED';
  assignedTo: string | null;
  assignee: { id: string; name: string; email: string; avatarUrl: string | null; role: string } | null;
  dueDate: string | null;
  dealId: string | null;
  contactId: string | null;
  createdAt: string;
  updatedAt: string;
}

// ai.summary.ready — пурраи AiSummary record
interface AiSummaryReadyPayload {
  id: string;
  communicationId: string;
  summary: string;
  sentiment: 'POSITIVE' | 'NEUTRAL' | 'NEGATIVE' | null;
  actionItems: { tasks: string[]; importantDates: string[]; dealStage: string | null } | null;
  nextFollowUp: string | null;
  generatedAt: string;
}

// deal.won — пурраи Deal record баъд аз update
interface DealWonPayload {
  id: string;
  companyId: string;
  contactId: string;
  title: string;
  value: string; // Decimal сериализатсия ҳамчун string
  currency: string;
  stage: 'WON';
  probability: number; // 100
  expectedCloseDate: string | null;
  ownerId: string | null;
  createdAt: string;
  updatedAt: string;
}
```

### Мисоли гӯш кардан дар клиент

```ts
socket.on('whatsapp.message.new', (payload: WhatsappMessageNewPayload) => {
  // масалан: inbox-ро refresh кун, toast нишон деҳ
});

socket.on('deal.won', (payload: DealWonPayload) => {
  // confetti 🎉, dashboard revenue-ро revalidate кун
});
```

## Дигар module-ҳо чӣ тавр event мефиристанд

Ҳар service-е, ки бояд event фиристад, `NotificationsService`-ро (на `NotificationsGateway`-ро
мустақим) inject мекунад ва module-и он бояд `NotificationsModule`-ро import кунад:

```ts
// xxx.module.ts
@Module({
  imports: [NotificationsModule],
  ...
})

// xxx.service.ts
constructor(private readonly notifications: NotificationsService) {}

this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.SOME_EVENT, payload);
```
