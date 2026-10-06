// VITE_API_BASE_URL is required and has no fallback. Vite embeds it at build time; if it is missing or not
// an absolute http(s) URL, the app reports a configuration error instead of guessing an address.
export type AppConfig = { ok: true; apiBaseUrl: string } | { ok: false; error: string };

export function readConfig(rawBaseUrl: string | undefined): AppConfig {
  const value = rawBaseUrl?.trim();
  if (!value) {
    return { ok: false, error: 'VITE_API_BASE_URL is not set, so the app cannot reach the extraction service.' };
  }

  try {
    const url = new URL(value);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      throw new Error('unsupported protocol');
    }
  } catch {
    return { ok: false, error: 'VITE_API_BASE_URL is not a valid http(s) URL.' };
  }

  return { ok: true, apiBaseUrl: value.replace(/\/+$/, '') };
}

export const config = readConfig(import.meta.env.VITE_API_BASE_URL);
