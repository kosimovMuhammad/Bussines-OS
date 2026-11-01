// REFERENCE implementation for the Next.js 15 frontend repo (copy + adapt). Not compiled here.
// Backend contract: docs/WHATSAPP_EMBEDDED_SIGNUP.md. All request bodies are camelCase JSON.
'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

declare global {
  interface Window {
    FB?: any;
    fbAsyncInit?: () => void;
  }
}

type SignupConfig = { appId: string; configurationId: string; graphVersion: string; signupVersion: string; state: string };
type SessionData = {
  event?: string;
  phoneNumberId?: string;
  wabaId?: string;
  messagingAccountIds?: string[];
  businessId?: string;
  isOnBizApp?: boolean;
};
export type WhatsAppAccount = {
  id: string; phoneNumberId: string; wabaId: string; verifiedName?: string | null; displayPhoneNumber?: string | null;
  isActive: boolean; isOnBizApp: boolean; connectionStatus: 'Active' | 'NeedsAttention';
  historySyncStatus: string; onboardedAt?: string | null; createdAt: string;
};
export type ConnectResult = { account: WhatsAppAccount; issues: string[] };

// Replace with your authenticated fetch wrapper (adds the bearer token, throws on !ok with the parsed body).
declare function api<T>(path: string, init?: { method?: string; body?: unknown }): Promise<T>;

const SDK_URL = 'https://connect.facebook.net/en_US/sdk.js';
const SESSION_EVENT_GRACE_MS = 3000; // the code lives ~30 s; the server derives ids from the token anyway

// Errors from our API look like { statusCode, error, message: { code, message } }.
export function errorCodeOf(e: any): string {
  return e?.body?.message?.code ?? e?.message?.code ?? 'unknown';
}

function loadSdk(config: SignupConfig): Promise<void> {
  return new Promise((resolve, reject) => {
    if (window.FB) { resolve(); return; }
    window.fbAsyncInit = () => {
      window.FB.init({ appId: config.appId, autoLogAppEvents: true, xfbml: true, version: config.graphVersion });
      resolve();
    };
    const script = document.createElement('script');
    script.src = SDK_URL;
    script.async = true;
    script.defer = true;
    script.crossOrigin = 'anonymous';
    script.onerror = () => reject(new Error('sdk_load_failed'));
    document.body.appendChild(script);
  });
}

export function useWhatsAppSignup(locale: string, onConnected: (r: ConnectResult) => void) {
  const [busy, setBusy] = useState(false);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const config = useRef<SignupConfig | null>(null);
  const session = useRef<SessionData | null>(null);
  const sdkReady = useRef(false);

  // Fetch config on mount and every ~5 min: FB.login must run synchronously inside the click handler,
  // and the signed state is only valid for 10 minutes.
  const refreshConfig = useCallback(async () => {
    try {
      config.current = await api<SignupConfig>(`/whatsapp/embedded-signup/config?locale=${encodeURIComponent(locale)}`);
      if (!sdkReady.current) {
        await loadSdk(config.current);
        sdkReady.current = true;
      }
    } catch (e) {
      setErrorCode(e instanceof Error && e.message === 'sdk_load_failed' ? 'sdk_load_failed' : errorCodeOf(e));
    }
  }, [locale]);

  useEffect(() => {
    refreshConfig();
    const t = setInterval(refreshConfig, 5 * 60 * 1000);
    return () => clearInterval(t);
  }, [refreshConfig]);

  // WA_EMBEDDED_SIGNUP session events from the popup.
  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      if (!event.origin.endsWith('facebook.com')) return;
      let msg: any;
      try { msg = typeof event.data === 'string' ? JSON.parse(event.data) : event.data; } catch { return; }
      if (msg?.type !== 'WA_EMBEDDED_SIGNUP') return;

      if (msg.event === 'CANCEL' || msg.event === 'ERROR') {
        const d = msg.data ?? {};
        api<{ code: string }>('/whatsapp/embedded-signup/client-event', {
          method: 'POST',
          body: { event: msg.event, currentStep: d.current_step, errorCode: d.error_code != null ? String(d.error_code) : undefined,
                  errorMessage: d.error_message, sessionId: d.session_id },
        }).then((r) => setErrorCode(r.code), () => setErrorCode(msg.event === 'CANCEL' ? 'cancelled' : 'unknown'))
          .finally(() => setBusy(false));
        session.current = null;
        return;
      }

      const d = msg.data ?? {};
      session.current = {
        event: msg.event, // FINISH | FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING | FINISH_ONLY_WABA | FINISH_GRANT_ONLY_API_ACCESS
        phoneNumberId: d.phone_number_id,
        wabaId: d.waba_id,
        messagingAccountIds: d.messaging_account_ids,
        businessId: d.business_id,
        isOnBizApp: d.is_on_biz_app,
      };
    };
    window.addEventListener('message', onMessage);
    return () => window.removeEventListener('message', onMessage);
  }, []);

  const complete = useCallback(async (code: string, cfg: SignupConfig) => {
    // Give the FINISH* event a moment to arrive, but never let the 30 s code run out.
    const started = Date.now();
    while (!session.current && Date.now() - started < SESSION_EVENT_GRACE_MS) {
      await new Promise((r) => setTimeout(r, 100));
    }
    try {
      const result = await api<ConnectResult>('/whatsapp/embedded-signup/complete', {
        method: 'POST',
        body: { code, state: cfg.state, session: session.current ?? undefined },
      });
      onConnected(result);
    } catch (e) {
      setErrorCode(errorCodeOf(e));
    } finally {
      session.current = null;
      setBusy(false);
      refreshConfig(); // the state is single-purpose; get a fresh one for the next attempt
    }
  }, [onConnected, refreshConfig]);

  // Call this from the button's onClick, directly (no await before FB.login).
  const connect = useCallback(() => {
    const cfg = config.current;
    if (!cfg || !window.FB) { setErrorCode('sdk_load_failed'); return; }
    setErrorCode(null);
    setBusy(true);
    session.current = null;

    window.FB.login(
      (response: any) => {
        const code = response?.authResponse?.code;
        if (code) {
          complete(code, cfg);
        } else {
          // Closed/denied without a code. If a CANCEL/ERROR event arrives it reports itself and clears busy;
          // otherwise report a plain cancel after a short wait.
          setTimeout(() => {
            setBusy((b) => {
              if (b) {
                api('/whatsapp/embedded-signup/client-event', { method: 'POST', body: { event: 'CANCEL' } }).catch(() => {});
                setErrorCode('cancelled');
              }
              return false;
            });
          }, 1500);
        }
      },
      {
        config_id: cfg.configurationId,
        response_type: 'code',
        override_default_response_type: true,
        // v5: WhatsApp Business app onboarding (Coexistence) is on by default - no featureType needed.
        extras: { setup: {}, version: cfg.signupVersion },
      },
    );
  }, [complete]);

  return { connect, busy, errorCode, clearError: () => setErrorCode(null) };
}

// UI: t(`whatsapp.errors.${errorCode}`) with the ru/tj/en JSON files next to this one.
// Retry: api(`/whatsapp/accounts/${id}/retry`, { method: 'POST' }) -> ConnectResult
// Disconnect: api(`/whatsapp/accounts/${id}`, { method: 'DELETE' })
