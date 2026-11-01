# WhatsApp connection via Meta Embedded Signup

Users never type a `phone_number_id`, WABA id or token. They click **Connect WhatsApp**, a Meta popup
opens, they log in with Facebook, add/verify their number and the backend receives everything itself.
Two kinds of number are supported by the same flow:

* **Standard** numbers — verified by SMS/voice code in the popup; the backend registers them for Cloud API.
* **Coexistence** numbers — already active in the **WhatsApp Business app**. The popup shows a code/QR in
  the Business app, the number keeps working on the phone, and the backend skips registration.

Do not use any unofficial WhatsApp Web / QR libraries — everything below is the official Cloud API.

---

## 1. Meta dashboard setup (app "Business OS")

Menu names move around in Meta's dashboard; if one differs, look for the same wording.

### 1.1 Products
1. App Dashboard → **Add product** → **WhatsApp** (set up if not already).
2. App Dashboard → **Add product** → **Facebook Login for Business**.

### 1.2 Facebook Login for Business → Settings → *Client OAuth settings*
Set **all** of these to **Yes**:

| Setting | Value |
|---|---|
| Client OAuth login | Yes |
| Web OAuth login | Yes |
| Enforce HTTPS | Yes |
| Embedded Browser OAuth Login | Yes |
| Use Strict Mode for redirect URIs | Yes |
| Login with the JavaScript SDK | Yes |

* **Allowed domains for the JavaScript SDK**: `https://localhost:5173` (and later your production frontend origin).
* **Valid OAuth redirect URIs**: the same origin(s), e.g. `https://localhost:5173/`.
* Meta only supports **HTTPS** here. If Meta rejects `localhost`, use the fallback in §3.3
  (serve the frontend through an ngrok HTTPS URL and register that instead).

### 1.3 Facebook Login for Business → *Configurations* → Create configuration
* Either **Create from template** → *"WhatsApp Embedded Signup Configuration With 60 Expiration Token"*
  (recommended for the first test), **or** a custom configuration with the login variation
  **"WhatsApp Embedded Signup"**.
* Assets/permissions: select **only** `whatsapp_business_management` and `whatsapp_business_messaging`
  (add `business_management` only if the template pre-selects it).
* Save it and copy the **Configuration ID**.
* The backend reads the real token expiry from `debug_token` and stores it, so it works whichever token
  lifetime you pick. (A 60-day token will need the refresh strategy planned for Phase 5; irrelevant for the
  first test.)

### 1.4 The configuration ID goes into user-secrets
```
dotnet user-secrets set WHATSAPP_ES_CONFIG_ID <configuration id> --project apps/api/WebApi
```
Already set by you: `WHATSAPP_APP_ID`, `WHATSAPP_APP_SECRET` (main Business OS app), `WHATSAPP_VERIFY_TOKEN`
(the rotated 32-char value), `PUBLIC_URL`. Optional: `WHATSAPP_API_VERSION` (default `v26.0`).
Restart the API after setting the key (`scripts/dev.ps1`).

### 1.5 WhatsApp → Configuration → Webhook
* **Callback URL**: `https://preacher-wavy-energetic.ngrok-free.dev/webhooks/whatsapp`
* **Verify token**: the **new** 32-char `WHATSAPP_VERIFY_TOKEN` from user-secrets (the old value in the
  dashboard no longer matches, so verification fails until you update it). The API must be running when you
  click *Verify and save* — Meta calls it immediately.
* **Subscribe** (Webhook fields of the *WhatsApp Business Account* object):

| Field | Why |
|---|---|
| `messages` | inbound messages + delivery statuses |
| `account_update` | partner added/removed, disconnects, `PARTNER_REMOVED`, `ACCOUNT_DELETED` |
| `smb_message_echoes` | **Coexistence**: messages sent from the Business app / companion devices |
| `history` | **Coexistence**: past message history after onboarding |
| `smb_app_state_sync` | **Coexistence**: contacts sync |
| `message_template_status_update` | optional, for the templates phase |
| `phone_number_quality_update` | optional |

  These are app-level subscriptions. The backend additionally calls `POST /{waba}/subscribed_apps` for each
  connected WABA, which is what makes Meta deliver that customer's events to this app.

### 1.6 What your own number needs (test now) vs real customers (later)
**Test now, with your own number (Coexistence):** you are admin of the app; WhatsApp Business app version
**2.24.17 or newer** (yours is 2.26.42); the number active in the Business app. No Tech Provider
status/App Review is *documented* as required for app admins testing their own assets — but the app is **Live**,
and Meta says that in Live mode only advanced-access-approved permissions appear in the flow. Whether that blocks
an admin's own number is not documented, so this is tested empirically: if the popup shows no
WhatsApp permissions or errors before the phone step, create a second app in **Development** mode with the same
setup and use its ids for testing.

**Only for real customers:** Tech Provider (or Solution Partner) enrollment, Business Verification, App Review
with advanced access for `whatsapp_business_management` + `whatsapp_business_messaging`, Access Verification
(onboarding limit 10 customers / 7 days → 200), privacy policy + deauthorize/data-deletion URLs, and customers
adding a payment method to their WABA.

**Eligibility of your number** (Coexistence): Meta's docs list no "active for N days" rule and no country list
(Tajikistan is neither listed nor excluded). Meta decides in the popup and answers with an error:
`3441045` (more activity needed), `3441042` (region unavailable), `4563015` (update the Business app),
`2655095`/`2655048` (number not registered with WhatsApp Business). See §4.

---

## 2. Backend API (all under `/whatsapp`, bearer-authenticated, JSON **camelCase**)

| Method & path | Purpose |
|---|---|
| `GET /embedded-signup/config?locale=ru` | `{ appId, configurationId, graphVersion, signupVersion, state }` — everything `FB.login` needs. `state` is HMAC-signed, bound to the user + company, valid 10 min. |
| `POST /embedded-signup/complete` | body `{ code, state, session }` → `{ account, issues }` |
| `POST /embedded-signup/client-event` | body `{ event: "CANCEL"|"ERROR", currentStep, errorCode, errorMessage, sessionId }` → `{ code, message }` |
| `POST /accounts/{id}/retry` | re-run register/subscribe for an account in `NeedsAttention` → `{ account, issues }` |
| `GET /accounts` | list accounts |
| `DELETE /accounts/{id}` | disconnect (hard delete, frees the number) |

`session` mirrors Meta's `WA_EMBEDDED_SIGNUP` message event, **all fields optional hints**:
`{ event, phoneNumberId, wabaId, messagingAccountIds, businessId, isOnBizApp }`. The server derives the real
WABA and number from the token and rejects hints that don't match (`asset_mismatch`). A snake_case body
(`phone_number_id`) does **not** bind.

`account` fields: `id, companyId, phoneNumberId, wabaId, verifiedName, displayPhoneNumber, isActive, isOnBizApp,
connectionStatus ("Active"|"NeedsAttention"), historySyncStatus ("NotApplicable"|"Pending"|"Requested"|"Completed"|"Declined"|"Failed"),
onboardedAt, tokenExpiresAt, createdAt`. `issues` is a list of stable codes for post-signup steps that failed
(`register_failed`, `register_rate_limited`, `subscribe_failed`).

**Errors.** Onboarding failures return `{ statusCode, error, message: { code, message } }` (same shape as plan-limit errors).
Always switch on `message.code`; `message.message` is an English fallback. Validation failures are
`422` with an array of messages. Removed endpoints: `POST /whatsapp/accounts`, `PATCH /whatsapp/accounts/{id}`,
`POST /whatsapp/embedded-signup`.

### Coexistence history sync — read this
After a Coexistence onboarding Meta allows **one** contacts sync and **one** history sync, only within **24 hours**
of onboarding. Phase 2 deliberately does **not** request them (the webhooks that carry that data are processed in
a later phase; requesting now would waste the one-time sync). So `historySyncStatus` stays `Pending`, and the
number you connect in the Phase 2 test will **not** get its old history/contacts — messages sent and received
afterwards still flow. Re-onboarding after the later phase ships will fetch history.

---

## 3. Frontend spec (Next.js 15)

### 3.1 HTTPS dev setup
```bash
# package.json
"dev": "next dev --experimental-https -p 5173"
# .env.local
NEXT_PUBLIC_API_URL=http://localhost:5129
```
* `--experimental-https` generates a local certificate (it may ask to install a local CA — accept) and serves
  `https://localhost:5173`.
* The page is HTTPS while the API is `http://localhost:5129`. Browsers treat `localhost` as a secure context, so
  this is not blocked as mixed content; the backend already allows `https://localhost:5173` in Development CORS.
* Do **not** send `Cross-Origin-Opener-Policy: same-origin` (a Next/`headers()` setting or middleware): the
  Meta popup needs `window.opener`; if you must set COOP use `same-origin-allow-popups`. Symptom of getting this
  wrong: the `FB.login` callback fires immediately and no session event arrives.
* Add `https://localhost:5173` to the Facebook Login for Business domains (§1.2).

### 3.2 Settings → Integrations → WhatsApp tab
**Remove the manual form** (phone number id / WABA id / token inputs) and any `PATCH` account call.

States (from `GET /whatsapp/accounts`):
* *Not connected* — explanation + **Connect WhatsApp** button.
* *Connected* — display number, verified name, badge **"Business app (Coexistence)"** when `isOnBizApp`, status chip,
  **Disconnect** (confirm dialog).
* *Needs attention* (`connectionStatus === "NeedsAttention"`) — warning + **Retry** (`POST /accounts/{id}/retry`).
* *Connecting* — button disabled with spinner from click until `/complete` resolves.
* Coexistence note under the badge (i18n): "Keep using WhatsApp Business on your phone. Messages sent from the app
  appear here once echo sync is enabled; history import is not available for this connection yet."

### 3.3 Connect flow
1. On tab mount call `GET /whatsapp/embedded-signup/config?locale=<current locale>` and **keep the result**; refresh it
   every ~5 min while the tab is open (state lives 10 min). `FB.login` must run **synchronously in the click
   handler** or popup blockers kill it — so never fetch inside the click.
2. Load the SDK once: `https://connect.facebook.net/en_US/sdk.js`, then
   `FB.init({ appId, autoLogAppEvents: true, xfbml: true, version: graphVersion })`.
3. Register a `window` `message` listener *before* login:
   ```ts
   window.addEventListener('message', (event) => {
     if (!event.origin.endsWith('facebook.com')) return;
     let data; try { data = JSON.parse(event.data); } catch { return; }
     if (data?.type !== 'WA_EMBEDDED_SIGNUP') return;
     // data.event: FINISH | FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING | FINISH_ONLY_WABA | FINISH_GRANT_ONLY_API_ACCESS | CANCEL | ERROR
     // success: data.data = { phone_number_id, is_on_biz_app, messaging_account_ids (v5; older: waba_id), business_id }
     // CANCEL:  data.data = { current_step }
     // ERROR:   data.data = { error_message, error_code, session_id, timestamp }
   });
   ```
4. Click handler:
   ```ts
   FB.login(callback, {
     config_id: config.configurationId,
     response_type: 'code',
     override_default_response_type: true,
     extras: { setup: {}, version: config.signupVersion },   // "v5": WhatsApp Business app onboarding is on by default
   });
   ```
   `response.authResponse.code` is the one-time code and **lives ~30 seconds**.
5. The code callback and the `FINISH*` message event arrive in either order. When the code arrives, wait **at most ~3 s**
   for the `FINISH`/`FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING` event, then immediately
   `POST /whatsapp/embedded-signup/complete` with
   `{ code, state: config.state, session: { event, phoneNumberId: data.phone_number_id, messagingAccountIds: data.messaging_account_ids, wabaId: data.waba_id, businessId: data.business_id, isOnBizApp: data.is_on_biz_app } }`
   (camelCase!). Sending no session is allowed; the server derives everything from the token.
6. No code (`authResponse` null) or a `CANCEL`/`ERROR` event: do **not** call `/complete`. Call
   `POST /whatsapp/embedded-signup/client-event` (so the failure + Meta's `session_id` are logged) and show the
   translation for the returned `code`.
7. Success: replace the tab content with the returned `account`; if `issues` is non-empty show the warning + Retry.
   After a fresh `GET /config` for the next attempt (the state is single-purpose).

**Fallback if Meta rejects `https://localhost:5173`:** run `ngrok http https://localhost:5173` (or a second reserved
domain), open the app via that HTTPS URL, register that origin in §1.2 and add it to the API's `CORS_ORIGIN`.

### 3.4 Error codes → i18n (`whatsapp.errors.<code>`)
Codes are a stable contract (never renamed). Russian/Tajik below are drafts — have a native speaker review.

| code | en | ru (draft) | tj (draft) |
|---|---|---|---|
| `cancelled` | Connection was cancelled. | Подключение отменено. | Пайвастшавӣ бекор карда шуд. |
| `number_not_eligible` | This number isn't eligible yet. Use the WhatsApp Business app more actively and try again later. | Этот номер пока не подходит. Поактивнее пользуйтесь приложением WhatsApp Business и повторите позже. | Ин рақам ҳоло мувофиқ нест. Бештар аз барномаи WhatsApp Business истифода баред ва баъдтар аз нав кӯшиш кунед. |
| `region_not_supported` | This feature is unavailable for numbers from this region. | Эта функция недоступна для номеров из этого региона. | Ин имконият барои рақамҳои ин минтақа дастрас нест. |
| `business_app_update_required` | Update the WhatsApp Business app to the latest version and try again. | Обновите приложение WhatsApp Business до последней версии и повторите. | Барномаи WhatsApp Business-ро то навтарин версия навсозӣ кунед ва аз нав кӯшиш кунед. |
| `number_not_on_business_app` | This number isn't registered in the WhatsApp Business app. | Этот номер не зарегистрирован в приложении WhatsApp Business. | Ин рақам дар барномаи WhatsApp Business сабт нашудааст. |
| `number_already_registered` | This number is registered elsewhere. Disconnect it there, wait ~15 minutes and try again. | Номер уже подключён в другом месте. Отключите его там, подождите около 15 минут и повторите. | Ин рақам дар ҷои дигар пайваст аст. Онро ҷудо кунед, тақрибан 15 дақиқа интизор шавед ва аз нав кӯшиш кунед. |
| `already_shared_with_partner` | This WhatsApp Business account is already shared with another partner. | Этот аккаунт WhatsApp Business уже передан другому партнёру. | Ин ҳисоби WhatsApp Business аллакай ба шарики дигар дода шудааст. |
| `marketing_messages_unsupported` | This account uses marketing messages, which isn't supported for this connection. | Этот аккаунт использует маркетинговые сообщения — для такого подключения это не поддерживается. | Ин ҳисоб паёмҳои маркетингӣ истифода мебарад, ки барои ин пайвастшавӣ дастгирӣ намешавад. |
| `already_connected` | This number is already connected to another company. | Этот номер уже подключён к другой компании. | Ин рақам аллакай ба ширкати дигар пайваст шудааст. |
| `invalid_state` | The request expired. Please start again. | Запрос устарел. Начните заново. | Дархост мӯҳлаташ гузашт. Аз нав оғоз кунед. |
| `invalid_session` / `unsupported_flow` | WhatsApp returned an unexpected result. Please try again. | WhatsApp вернул неожиданный результат. Попробуйте ещё раз. | WhatsApp натиҷаи ғайричашмдошт баргардонд. Аз нав кӯшиш кунед. |
| `code_exchange_failed` | The sign-in timed out. Please try again. | Время входа истекло. Попробуйте ещё раз. | Вақти ворид шудан гузашт. Аз нав кӯшиш кунед. |
| `invalid_token` | WhatsApp rejected the access. Please connect again. | WhatsApp отклонил доступ. Подключитесь заново. | WhatsApp дастрасиро рад кард. Аз нав пайваст шавед. |
| `no_waba_access` / `asset_mismatch` | No WhatsApp Business account was shared. Please try again and grant access. | Аккаунт WhatsApp Business не был предоставлен. Повторите и разрешите доступ. | Ҳисоби WhatsApp Business дода нашуд. Аз нав кӯшиш кунед ва дастрасӣ диҳед. |
| `no_phone_number` | No phone number was added. | Номер телефона не был добавлен. | Рақами телефон илова нашуд. |
| `ambiguous_phone_number` | Several numbers were shared; select one. | Предоставлено несколько номеров, выберите один. | Якчанд рақам дода шуд, яктоашро интихоб кунед. |
| `register_failed` | Saved, but registration with WhatsApp failed. Press Retry. | Сохранено, но регистрация в WhatsApp не удалась. Нажмите «Повторить». | Захира шуд, вале сабт дар WhatsApp ба иҷро нарасид. «Аз нав»-ро пахш кунед. |
| `register_rate_limited` | WhatsApp limits registration attempts. Wait a few hours and retry. | WhatsApp ограничивает число попыток регистрации. Подождите несколько часов. | WhatsApp шумораи кӯшишҳои сабтро маҳдуд мекунад. Якчанд соат интизор шавед. |
| `subscribe_failed` | Saved, but webhook subscription failed. Press Retry. | Сохранено, но подписка на вебхуки не удалась. Нажмите «Повторить». | Захира шуд, вале обунаи вебхук ба иҷро нарасид. «Аз нав»-ро пахш кунед. |
| `not_configured` | WhatsApp onboarding isn't configured on the server. | Подключение WhatsApp не настроено на сервере. | Пайвасткунии WhatsApp дар сервер танзим нашудааст. |
| `provider_error` / `unknown` | WhatsApp rejected the request. Please try again. | WhatsApp отклонил запрос. Попробуйте ещё раз. | WhatsApp дархостро рад кард. Аз нав кӯшиш кунед. |

Meta's popup-side error codes mapped by `/client-event`: `3441045`, `3441042`, `4563015`, `2655095`, `2655048`
(plus `2655011`, `3441030`, `3441049`, `2655094`, `2655082`, `2655093`, `3441047`, `133016`, `190`).
