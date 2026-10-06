# Pluggable identity providers + provider-agnostic super-admin

Authentication is an abstraction (`IAuthProvider`), not a hard dependency on one IdP. Each provider owns one scheme and knows how to register it; the enabled set comes from config (`Auth:Providers`), inferring Entra when an `AzureAd` app registration is present and otherwise the Dev provider so a fresh checkout runs. Entra ID is the first real provider; adding Auth0/Okta/Google means adding one `IAuthProvider` and listing its key.

The app never reads a provider-specific claim directly. `CurrentUser` resolves oid/email/name/groups defensively across token shapes, and Super-Admin (global admin) elevation is provider-agnostic and rule-based: any of hard-coded oids, hard-coded emails, a security-group id in a `groups` claim, or an arbitrary claim type==value (`Auth:SuperAdmins`).

## Consequences

- Login flow specifics (BFF cookie vs bearer, CSRF, token refresh) are the provider's concern; the rest of the app only sees `CurrentUser`.
- Group-based elevation needs the IdP to emit a `groups` claim (Entra: configure the app registration's optional/group claims).
- The Dev provider authenticates every request as one identity — it must never be in the enabled set for a hardened deployment.
