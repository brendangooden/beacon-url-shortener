# Security policy

## Reporting a vulnerability

Please do not open a public issue for security problems.

Report them privately through GitHub:
**Security → Report a vulnerability** on this repository.

Include:
- What the problem is and where it is.
- Steps to reproduce it.
- The impact you expect.

I aim to reply within 7 days.

## Supported versions

Only the latest commit on `main` is supported.

## Deployment note

With no identity provider configured, Beacon runs the built-in Dev auth provider.
It signs every request in as a local user, so the admin surface is open.
Configure Microsoft Entra (see the README) before you expose Beacon beyond localhost.
