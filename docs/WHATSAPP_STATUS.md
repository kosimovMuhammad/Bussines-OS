# WhatsApp Embedded Signup - status (paused 2026-11-02)

**State: built, paused behind a flag.** `WHATSAPP_EMBEDDED_SIGNUP_ENABLED` defaults to `false`.
While it is false the config endpoint (`GET /whatsapp/embedded-signup/config`) answers
`not_configured` (503) and the WhatsApp tab shows a translated (ru/tj/en) "coming soon - requires
Meta business verification" card instead of the Connect button.

## What is built (all kept, all tested)

- Embedded Signup v5 with Coexistence: signed single-purpose `state`, `FB.login` with `config_id`,
  one-time code exchange (`ExchangeCodeAsync`, documented form: no `redirect_uri`, no retry),
  `debug_token` verification of the granted WABA, phone-number resolution, register / webhook
  subscribe, `NeedsAttention` + retry, disconnect, client CANCEL/ERROR event reporting.
- Frontend hook `useWhatsAppSignup` + panel, i18n (en/ru/tj) for every error code, including
  `continue_as_not_supported` and `embedded_signup_not_started`.
- Dev-only `[WA-ES]` step trace (`POST /whatsapp/embedded-signup/debug`, 404 outside Development,
  secrets redacted) written to `apps/api/WebApi/logs/dev-*.log`.
- Unit tests: `WhatsAppFoundationTests`, `WhatsAppOnboardingTests` (incl. flag-off => not_configured).

## What is blocked, and why

Live tests on 2026-11-01 (app id `1932227084123249`, config `2132775830657994`, `v25.0`/`v5`):

1. **The Meta app is not eligible to onboard customers** (not a Tech Provider; business
   verification / App Review / Advanced access for `whatsapp_business_management` and
   `whatsapp_business_messaging` not done). The Facebook Login for Business configuration editor only
   offers the "General" login variation, not "WhatsApp Embedded Signup".
2. **"Continue as <user>" path is not supported.** `FB.login` returns `status: connected` with a
   code, but no `WA_EMBEDDED_SIGNUP` event is sent (the WhatsApp screens never run). Exchanging that
   code fails with code 100 "redirect_uri is identical..." for EVERY variant tried (none, empty, page
   URL, origin, origin + `/`, `login_success.html`, `xd_arbiter`); the page-URL / xd_arbiter variants
   also returned 191 (domain not in App Domains). No redirect_uri value rescues it, so there is no retry.
3. **"Change settings" path** returned a token-only `authResponse` (`accessToken`, `signedRequest`, no
   `code`) and no event: Meta did not run Embedded Signup for this config/app.

Sources (Meta docs, `developers.facebook.com/documentation/business-messaging/whatsapp/`):
`embedded-signup/overview`, `embedded-signup/implementation`, `solution-providers/app-review`,
`solution-providers/get-started-for-tech-providers`. Note: no Meta page was found that states the
exact restriction wording or any `redirect_uri` guidance - the eligibility cause above is the best
fit, not a confirmed Meta statement.

## Steps to unblock

1. **Business verification** of the business portfolio that owns the app (App dashboard > Start
   verification; name, address, phone, email, website, documents).
2. **App Review** (Tech Provider path): app icon, privacy policy URL, category; screen recordings
   (message sent from the app and received in WhatsApp; template creation); request **Advanced access**
   for `whatsapp_business_management` and `whatsapp_business_messaging`. Make a successful Graph call with
   each permission first.
3. **Access Verification** (needed for the 200 customers/7 days limit; default is 10/7 days).
4. **Facebook Login for Business > Configurations**: create the configuration from the "WhatsApp
   Embedded Signup" variation (or the "...With 60 Expiration Token" template); select only the needed
   assets/permissions. New `config_id` => `WHATSAPP_ES_CONFIG_ID`.
5. **Facebook Login for Business > Settings > Client OAuth settings**: Client OAuth login, Web OAuth
   login, Enforce HTTPS, Embedded Browser OAuth Login, Use strict mode for redirect URIs, Login with
   the JavaScript SDK = Yes. List the frontend domain (an HTTPS tunnel, not plain localhost) in both
   "Allowed domains" and "Valid OAuth redirect URIs"; add it to App Domains.
6. Check the Embedded Signup version (v2/v3 are deprecated from 2026-10-15; we send `v5`).
7. **Testing before approval:** create a separate Development-mode Meta app, give testers an
   admin/developer/tester role, create its own `config_id`, and swap only user secrets:
   `WHATSAPP_APP_ID`, `WHATSAPP_APP_SECRET`, `WHATSAPP_ES_CONFIG_ID`, `WHATSAPP_VERIFY_TOKEN`,
   `PUBLIC_URL`; point the new app's webhook at `PUBLIC_URL/webhooks/whatsapp` (`whatsapp_business_account`).
   Unverified: whether Coexistence works in dev mode and whether non-role users can complete signup.
8. Set `WHATSAPP_EMBEDDED_SIGNUP_ENABLED=true` (user secrets / env) and restart the API.

## Retest checklist

- [ ] `GET /whatsapp/embedded-signup/config` returns 200 (not `not_configured`); panel shows Connect.
- [ ] Popup shows the WhatsApp screens (phone number / Business app Coexistence), not just "Continue as".
- [ ] `[WA-ES] message` log shows a `WA_EMBEDDED_SIGNUP` event (`FINISH*`) **and** `fb.login.response`
      has `codePresent: true`.
- [ ] Log shows `[2/7] one-time code exchanged`, `[3/7] token verified` with `grantedWabaIds`.
- [ ] Account appears Connected; webhook subscribed; test inbound message lands in Communications.
- [ ] Disconnect works; reconnect works; token expiry visible (`debug_token`).
- [ ] "Continue as" path shows the `continue_as_not_supported` message, "Change settings"/cancel show
      `cancelled`, token-only result shows `embedded_signup_not_started`.
- [ ] Repeat in ru / tj / en and at 390px width.
