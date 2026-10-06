# Contributing

Thanks for helping with Beacon.

## Before you start
- Open an issue first for large changes.
- Read `CONTEXT.md` for the domain terms and `docs/adr/` for key decisions.

## Set up
- Prerequisites: .NET 10 SDK, Node 24, Docker.
- Run the app: see "Local development" in the README.

## Check your change
```bash
dotnet test --solution beacon.slnx        # needs Docker running
cd frontend/admin-app && npm run lint && npm run build
```

## Rules
- Warnings fail the build. Fix them. If you must exempt a rule, do it in `.editorconfig` with a reason.
- Keep features isolated. The architecture tests enforce this.
- Add or update tests for behaviour changes.
- Keep pull requests small and focused.

## Licence
By contributing, you agree your work is released under the O'Saasy License (see LICENSE), and you grant the project owner the right to use, modify and relicense it in any version of Beacon, including closed-source and hosted versions.
