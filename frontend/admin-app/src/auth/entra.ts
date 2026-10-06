import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
  type Configuration,
} from "@azure/msal-browser";

// All optional, baked at build. When VITE_ENTRA_CLIENT_ID is empty the whole MSAL layer is inert
// and the app behaves exactly as in Dev mode (backend Dev auth authenticates every request).
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined;
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID as string | undefined;
const scope = import.meta.env.VITE_ENTRA_SCOPE as string | undefined;

export const isEntraConfigured = Boolean(clientId && tenantId && scope);
export const apiScopes: string[] = scope ? [scope] : [];

// Login request. `offline_access` asks Entra for a refresh token (now that it is consented), so MSAL
// can renew the API access token silently — via the refresh token, not a third-party-cookie iframe.
export const loginRequest = { scopes: [...apiScopes, "offline_access"] };

const config: Configuration | null = isEntraConfigured
  ? {
      auth: {
        clientId: clientId!,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        // The SPA is served under /admin on every host; all /admin redirect URIs are registered.
        redirectUri: `${window.location.origin}/admin`,
        postLogoutRedirectUri: `${window.location.origin}/admin`,
        // Process the auth response ON the redirect page (/admin) instead of navigating back to the
        // request URL. The app is served at /admin/ while the redirect URI is /admin — that trailing
        // slash mismatch made MSAL navigate instead of exchanging the code, causing a login loop.
        // We restore the user's intended deep-link path ourselves after sign-in (see main.tsx).
        navigateToLoginRequestUrl: false,
      },
      cache: {
        // localStorage persists the refresh token across tab/browser restarts, so offline_access keeps
        // the user signed in between sessions. Trade-off: tokens are readable by any XSS on the page —
        // acceptable for this internal, Entra-gated admin tool. Use "sessionStorage" (per-tab, cleared on
        // close) or "memory" (gone on reload) if a stricter posture is wanted.
        cacheLocation: "localStorage",
        storeAuthStateInCookie: false,
      },
    }
  : null;

/** The single MSAL instance, or null in Dev mode. Shared by MsalProvider and the API client. */
export const msalInstance: PublicClientApplication | null = config
  ? new PublicClientApplication(config)
  : null;

function activeAccount(): AccountInfo | null {
  if (!msalInstance) return null;
  return msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0] ?? null;
}

// Refresh proactively while this much token lifetime remains, so a request never carries a
// near-expiry token — the backend then never 401s on expiry (which is what fed the login loop).
const REFRESH_SKEW_MS = 15 * 60 * 1000;

/** Bearer access token for the API, or null in Dev mode / when not signed in. */
export async function getAccessToken(): Promise<string | null> {
  if (!msalInstance || apiScopes.length === 0) return null;
  const account = activeAccount();
  if (!account) return null;
  try {
    let result = await msalInstance.acquireTokenSilent({ scopes: apiScopes, account });
    const msLeft = result.expiresOn ? result.expiresOn.getTime() - Date.now() : 0;
    if (msLeft < REFRESH_SKEW_MS) {
      // Token expires within the skew window — force a silent refresh before we use it.
      result = await msalInstance.acquireTokenSilent({ scopes: apiScopes, account, forceRefresh: true });
    }
    return result.accessToken;
  } catch (error) {
    // Only a genuine "interaction required" (session gone) bounces to interactive auth — never a
    // plain API 401 (that would loop). Silent-refresh failures otherwise just send no token.
    if (error instanceof InteractionRequiredAuthError) {
      await msalInstance.acquireTokenRedirect({ scopes: apiScopes, account });
    }
    return null;
  }
}

// --- Proactive silent renewal ------------------------------------------------------------------
// Reactive renewal (getAccessToken) refreshes on the next API call; this keeps a fresh token even
// while the tab sits idle, and keeps the refresh-token chain rotating so the session stays alive.
// Entra rotates the refresh token on each use and caps a SPA's chain at ~24h, so an active user is
// renewed seamlessly; only real inactivity past that window falls back to interactive sign-in.
const RENEW_LEAD_MS = 5 * 60 * 1000; // renew this long before the access token expires
let renewalTimer: ReturnType<typeof setTimeout> | undefined;

/** Start the background renewal loop. Safe to call more than once (it reschedules a single timer). */
export function startTokenRenewal(): void {
  if (!msalInstance || apiScopes.length === 0) return;

  const tick = async () => {
    const account = activeAccount();
    if (!account) return;
    try {
      // forceRefresh guarantees a round-trip through the refresh token rather than a cached copy.
      const result = await msalInstance!.acquireTokenSilent({ scopes: apiScopes, account, forceRefresh: true });
      const msLeft = result.expiresOn ? result.expiresOn.getTime() - Date.now() : 60 * 60 * 1000;
      schedule(Math.max(30_000, msLeft - RENEW_LEAD_MS));
    } catch (error) {
      // Session genuinely gone → stop; the next user action / getAccessToken bounces to interactive.
      // Anything transient → retry shortly.
      if (!(error instanceof InteractionRequiredAuthError)) {
        schedule(60_000);
      }
    }
  };

  const schedule = (delay: number) => {
    clearTimeout(renewalTimer);
    renewalTimer = setTimeout(() => void tick(), delay);
  };

  void tick();
}

/** Force interactive re-auth (e.g. after a 401). No-op in Dev mode. */
export async function reauthenticate(): Promise<void> {
  if (!msalInstance) return;
  const account = activeAccount();
  await msalInstance.acquireTokenRedirect({ scopes: apiScopes, account: account ?? undefined });
}

export function signOut(): void {
  void msalInstance?.logoutRedirect();
}

export function signedInName(): string | null {
  const account = activeAccount();
  return account?.name ?? account?.username ?? null;
}
