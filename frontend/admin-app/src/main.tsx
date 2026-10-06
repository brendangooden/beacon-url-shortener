import { StrictMode, type ReactNode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import { MsalProvider } from "@azure/msal-react";
import { DialogProvider } from "@/components/dialogs";
import { ReleaseBanner } from "@/components/ReleaseBanner";
import { router } from "@/router";
import { loginRequest, msalInstance, startTokenRenewal } from "@/auth/entra";
import "@/styles.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 15_000, retry: 1, refetchOnWindowFocus: false },
  },
});

function AppTree() {
  const app = (
    <QueryClientProvider client={queryClient}>
      <DialogProvider>
        <RouterProvider router={router} />
        <ReleaseBanner />
      </DialogProvider>
    </QueryClientProvider>
  );
  // MsalProvider is only mounted when Entra is configured; Dev mode renders the bare app.
  return msalInstance ? <MsalProvider instance={msalInstance}>{app}</MsalProvider> : app;
}

function SigningIn() {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 bg-[var(--bg)] text-[var(--muted)]">
      <div className="h-6 w-6 animate-spin rounded-full border-2 border-[var(--border)] border-t-[var(--accent)]" />
      <p className="text-sm">Signing in…</p>
    </div>
  );
}

function renderRoot(node: ReactNode) {
  const el = document.getElementById("root");
  if (!el) throw new Error("Root element not found");
  createRoot(el).render(<StrictMode>{node}</StrictMode>);
}

// Preserve the intended /admin deep link across the Entra redirect (navigateToLoginRequestUrl is off).
const RETURN_TO_KEY = "beacon.returnTo";
function rememberDeepLink() {
  try {
    sessionStorage.setItem(RETURN_TO_KEY, window.location.pathname + window.location.search);
  } catch {
    /* sessionStorage unavailable — deep link won't be restored, not fatal */
  }
}
function restoreDeepLink() {
  try {
    const target = sessionStorage.getItem(RETURN_TO_KEY);
    sessionStorage.removeItem(RETURN_TO_KEY);
    const here = window.location.pathname + window.location.search;
    if (target && target.startsWith("/admin") && target !== here) {
      window.history.replaceState(null, "", target);
    }
  } catch {
    /* ignore */
  }
}

async function bootstrap() {
  // Dev mode (no Entra config): render exactly as before, no MSAL, no login.
  if (!msalInstance) {
    renderRoot(<AppTree />);
    return;
  }

  await msalInstance.initialize();

  // MSAL hidden-iframe guard (silent renew loads the redirectUri in an iframe): let MSAL parse the
  // response but do NOT mount the app inside the iframe — avoids the block_iframe_reload loop.
  const inIframe = window !== window.parent;
  const hasAuthResponse = /[?#].*(code=|error=|state=)/.test(window.location.href);
  if (inIframe && hasAuthResponse) {
    await msalInstance.handleRedirectPromise();
    return;
  }

  const response = await msalInstance.handleRedirectPromise();
  if (response?.account) {
    msalInstance.setActiveAccount(response.account);
  }

  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0] ?? null;
  if (account) {
    msalInstance.setActiveAccount(account);
    startTokenRenewal(); // keep the access token fresh via the refresh token while signed in
    restoreDeepLink(); // navigateToLoginRequestUrl is off, so return the user to where they were headed
    renderRoot(<AppTree />);
    return;
  }

  // Not signed in → remember the intended deep link, show a splash, and redirect to Entra.
  rememberDeepLink();
  renderRoot(<SigningIn />);
  await msalInstance.loginRedirect(loginRequest);
}

void bootstrap();
