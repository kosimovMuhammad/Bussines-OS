export interface IncomingWhatsappMessage {
  providerMessageId: string;
  phoneNumberId: string;
  from: string;
  text: string;
  timestamp: number;
}

interface MetaWebhookPayload {
  entry?: {
    changes?: {
      value?: {
        metadata?: { phone_number_id?: string };
        messages?: {
          id?: string;
          from?: string;
          timestamp?: string;
          type?: string;
          text?: { body?: string };
        }[];
      };
    }[];
  }[];
}

export function extractIncomingMessages(payload: MetaWebhookPayload): IncomingWhatsappMessage[] {
  const result: IncomingWhatsappMessage[] = [];

  for (const entry of payload.entry ?? []) {
    for (const change of entry.changes ?? []) {
      const phoneNumberId = change.value?.metadata?.phone_number_id;
      if (!phoneNumberId) continue;

      for (const message of change.value?.messages ?? []) {
        if (!message.id || !message.from || !message.timestamp) continue;
        result.push({
          providerMessageId: message.id,
          phoneNumberId,
          from: message.from,
          text: message.type === 'text' ? (message.text?.body ?? '') : `[${message.type ?? 'unsupported'}]`,
          timestamp: Number(message.timestamp),
        });
      }
    }
  }

  return result;
}
