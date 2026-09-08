# Kosova Rent-A-Car

A modular rental marketplace: Next.js 16 / React 19 / TypeScript, ASP.NET Core 10, PostgreSQL 17. Professional rental businesses, exact vehicles, request-to-book. No private-owner rentals or instant booking.

## Local preview

- Website: http://127.0.0.1:3000/en
- Supplier/admin sign-in: http://127.0.0.1:3000/en/login
- API health: http://127.0.0.1:5080/api/health
- Languages: `/en`, `/sq`, `/de`.
- Development accounts: `supplier@demo.local`, `admin@demo.local`, `other@demo.local`. The generated password is in `.local/runtime/demo-password.txt`; it is deliberately not committed or embedded in the UI.

This workspace has a portable PostgreSQL installation under `.local/postgres` and its isolated data directory under `.local/pgdata`. Start it with `scripts/start-local.ps1`. No Docker or cloud account is required for this already-provisioned workspace.

## Setup on another machine

1. Install Node 22+, .NET SDK 10, and PostgreSQL 17 (or use `infra/compose.yaml`).
2. Create an empty database and set the environment variables in `.env.example`. ASP.NET reads environment variables directly; it does not automatically read `.env`.
3. Run `npm ci --prefix apps/web` and `dotnet restore apps/api`.
4. Run `dotnet run --project apps/api --no-launch-profile --urls http://127.0.0.1:5080` with the API environment set. Versioned SQL migrations run under a PostgreSQL advisory lock. Development seed data is opt-in (`DemoMode=true`) and never seeds a nonempty supplier table.
5. Run `npm run dev --prefix apps/web`, or `npm run build --prefix apps/web` followed by `npm run start --prefix apps/web`.
6. Visit the local preview. Use a separate database for tests.

## Verification

- `dotnet test tests/Kosova.Tests` — deterministic business-rule tests.
- From `apps/web`, `node tests/integration.mjs` — actual PostgreSQL/API integration and concurrency tests, using `kosova_test` on port 55432 and API port 5081 by default. Local credentials are read from ignored runtime files; CI can supply environment variables.
- From `apps/web`, `npx playwright test` — real browser customer/supplier journeys, three mobile locales, keyboard focus, axe accessibility checks, admin surfaces.
- `npm run typecheck --prefix apps/web` and `npm run build --prefix apps/web`.
- `scripts/backup-restore.ps1` — isolated database dump/restore verification.

See `docs/PROGRESS.md` for verified results and remaining dependencies. See `docs/LAUNCH.md` before enabling production services or accepting real bookings.

## Public preview

- Frontend: https://kosova-rent-a-car.vercel.app/en
- Source: https://github.com/Arnis2002/kosova-rent-a-car

The public Vercel deployment is a frontend preview with draft legal content and no real inventory photos. The ASP.NET API and PostgreSQL database still require a separate production host and provider configuration before public search, booking, supplier and administrator operations can be enabled.

## Documentation

- `docs/ARCHITECTURE.md`: relationships, pricing, status transitions and invariants.
- `docs/API.md`: API contracts and authorization.
- `docs/OPERATIONS.md`: supplier/admin operating procedures.
- `docs/DEPLOYMENT.md`: environment, rollout, backups and rollback.
- `docs/LAUNCH.md`: external dependencies and launch checklist.

## Images

The UI uses empty placeholders until you upload your own vehicle photos. Upload JPEG/PNG through Documents with kind Photo; an administrator reviews it; use Photo to select the approved image for the vehicle. Real uploaded photos must represent the exact vehicle. No generated image is included or referenced by the application.
