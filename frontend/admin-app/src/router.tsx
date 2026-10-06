import { createRootRoute, createRoute, createRouter } from "@tanstack/react-router";
import { AppShell } from "@/components/AppShell";
import { AllLinksPage } from "@/routes/AllLinksPage";
import { AnalyticsPage } from "@/routes/AnalyticsPage";
import { TrashPage } from "@/routes/TrashPage";
import { LinkAnalyticsPage } from "@/routes/LinkAnalyticsPage";
import { LinksPage } from "@/routes/LinksPage";
import { SettingsPage } from "@/routes/SettingsPage";
import { SharedWithMePage } from "@/routes/SharedWithMePage";

const rootRoute = createRootRoute({ component: AppShell });

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: LinksPage,
  // ?folder={id} makes a folder view hotlinkable / bookmarkable.
  validateSearch: (search: Record<string, unknown>): { folder?: string } => ({
    folder: typeof search.folder === "string" && search.folder ? search.folder : undefined,
  }),
});
const analyticsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/analytics", component: AnalyticsPage });
const allLinksRoute = createRoute({ getParentRoute: () => rootRoute, path: "/all-links", component: AllLinksPage });
const trashRoute = createRoute({ getParentRoute: () => rootRoute, path: "/trash", component: TrashPage });
const sharedRoute = createRoute({ getParentRoute: () => rootRoute, path: "/shared", component: SharedWithMePage });
const settingsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/settings", component: SettingsPage });

const linkAnalyticsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/links/$linkId",
  // ?ws=<workspaceId> lets a shared link resolve against its own workspace, not the selected one.
  validateSearch: (search: Record<string, unknown>): { ws?: string } => ({
    ws: typeof search.ws === "string" ? search.ws : undefined,
  }),
  component: LinkAnalyticsRoute,
});

function LinkAnalyticsRoute() {
  const { linkId } = linkAnalyticsRoute.useParams();
  const { ws } = linkAnalyticsRoute.useSearch();
  return <LinkAnalyticsPage linkId={linkId} workspaceIdOverride={ws} />;
}

const routeTree = rootRoute.addChildren([indexRoute, analyticsRoute, allLinksRoute, trashRoute, sharedRoute, settingsRoute, linkAnalyticsRoute]);

export const router = createRouter({ routeTree, basepath: "/admin", trailingSlash: "never" });

// TanStack renders the index route under a basepath as "/admin/"; trailingSlash:"never" doesn't strip
// that base slash. Normalize "/admin/" -> "/admin" after each resolved navigation (query/hash kept).
if (typeof window !== "undefined") {
  const normalizeBaseSlash = () => {
    const { pathname, search, hash } = window.location;
    if (pathname === "/admin/") {
      window.history.replaceState(window.history.state, "", "/admin" + search + hash);
    }
  };
  normalizeBaseSlash();
  router.subscribe("onResolved", normalizeBaseSlash);
}

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
