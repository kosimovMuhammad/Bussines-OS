// Normalizes user-entered phone numbers to E.164 before validation.
//
// Users commonly type international numbers with the "00" exit-code
// convention (e.g. "00992901234567") instead of the "+" E.164 prefix
// ("+992901234567"). Historically our contact DTOs only accepted a
// leading "+" and silently 422'd anything else, so a "00"-style number
// would never make it into the system correctly. This also strips
// spaces/dashes/parentheses so pasted numbers like "+992 90 123 45 67"
// validate correctly.
export function normalizePhone(value: unknown): unknown {
  if (typeof value !== 'string') return value;

  const trimmed = value.trim();
  if (!trimmed) return trimmed;

  const stripped = trimmed.replace(/[\s\-()]/g, '');

  if (stripped.startsWith('00')) {
    return `+${stripped.slice(2)}`;
  }

  return stripped;
}
