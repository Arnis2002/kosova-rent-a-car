# Supabase Test Deployment and Rental Partner Onboarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the existing .NET API against the user's Supabase PostgreSQL project for test use and let rental businesses apply, receive approval, and manage draft vehicles.

**Architecture:** Keep the existing blue Next.js app on Vercel and deploy the existing .NET 10 API from `infra/Api.Dockerfile` as a separate container service. Use Supabase PostgreSQL and Supabase Auth; the API validates Auth tokens and remains authoritative for supplier membership/roles. Apply a non-production hosted-test mode with uploads and real notifications disabled.

**Tech Stack:** .NET 10 / ASP.NET Core, Npgsql, PostgreSQL migrations, Supabase Auth and PostgreSQL, Next.js 16 / React 19, Docker, Vercel, free-tier container host.

**Spec:** `docs/superpowers/specs/2026-10-10-supabase-supplier-onboarding-design.md`

## Global Constraints

- Keep database passwords and Supabase service-role keys exclusively in private API-host settings; never commit or expose them in browser assets.
- Keep hosted testing separate from local `Development` and production; do not weaken production guards.
- Do not reset or delete the Supabase project; inspect the target schema before migrations and preserve any existing data.
- Hosted API uses Supabase session pooling or another verified connection that supports the session advisory lock used by migrations.
- Apply schema changes with Supabase's migration/admin connection before deploy; the running HostedTest API only verifies schema version 4 and `btree_gist`, and receives a separate DML-only login role with BYPASSRLS so browser roles remain blocked.
- Public registration cannot grant a supplier role or publish inventory; only admin approval maps a verified Supabase identity to an approved supplier owner.
- No real payments, outbound customer email, or document uploads without persistent private storage and scanning.
- Enable PostgreSQL RLS on every application table with no browser-facing policies; only the server-side .NET connection can access marketplace records.
- Keep the blue Rent Kosova design and existing API/schema behavior backward compatible for local tests.

## Review Focus

- Invalid, expired, wrong-issuer, and unverified Supabase tokens must not authenticate; test in API auth tests.
- Pending/rejected applications must not access supplier endpoints; test application and authorization integration cases.
- Cross-supplier IDs and attempts to publish from owner accounts must remain denied; extend existing API integration coverage.
- Hosted-test mode must start without production OIDC/S3/SMTP/certificate secrets but must not enable local demo login, uploads, or external notifications; add `Hosting` mode tests.
- Empty-result and duplicate applications must remain safe and informative; test database constraints and localized UI behavior.

---

### Task 1: Add an explicit hosted-test runtime mode

**Files:**
- Modify: `apps/api/Hosting.cs`
- Modify: `apps/api/Program.cs`
- Modify: `apps/api/Adapters.cs`
- Modify: `apps/api/OperationsWorker.cs`
- Modify: `apps/api/RuntimeOptions` declaration in `apps/api/Program.cs` (or extract if needed)
- Modify: `infra/Api.Dockerfile`
- Test: `tests/Kosova.Tests/`

**Interfaces:**
- Keep local `DemoMode=true` behavior limited to Development.
- Add explicit `RuntimeMode` values `DevelopmentDemo`, `HostedTest`, `Production`; configuration rejects unknown values and contradictory `DemoMode` combinations.
- `HostedTest` requires HTTPS `WebOrigin` and a Supabase Auth issuer/project URL, but not production-only OIDC, S3, scanner, SMTP, or certificate secrets.
- In `HostedTest`, uploads fail closed, notification delivery is suppressed, and data protection uses an ephemeral provider suitable only for test use.

- [ ] **Step 1: Add failing mode-validation tests** asserting local demo works only in Development, hosted-test works in Production ASP.NET environment with minimal test settings, unknown/conflicting modes fail, and production requirements remain enforced.
- [ ] **Step 2: Run** `dotnet test tests/Kosova.Tests/Kosova.Tests.csproj`; confirm the new tests fail for the current implementation.
- [ ] **Step 3: Implement the explicit mode and fail-closed hosted-test adapters** without changing production mode behavior.
- [ ] **Step 4: Add an API integration test** confirming hosted-test `/api/health` reports database connectivity and upload/notification side effects are disabled.
- [ ] **Step 5: Run** `dotnet test tests/Kosova.Tests/Kosova.Tests.csproj` and the local integration suite; confirm both local demo and hosted-test checks pass.

### Task 2: Add verified Supabase identity and supplier application schema/API

**Files:**
- Create: `apps/api/Sql/004_supplier_applications.sql`
- Modify: `apps/api/Kosova.Api.csproj`
- Modify: `apps/api/Hosting.cs`
- Modify: `apps/api/Program.cs`
- Modify: `apps/api/Security.cs`
- Modify: `apps/api/Management.cs`
- Test: `tests/Kosova.Tests/`
- Test: `apps/web/tests/integration.mjs`

**Interfaces:**
- Validate each Supabase bearer token through `GET {Supabase:Url}/auth/v1/user` using the project's public publishable key; accept only a successful response with a UUID user id, email, and confirmed email. This supports both legacy HS256 and asymmetric project signing modes without retrieving a JWT secret.
- Extend `Security.Actor` to accept only the identity claims produced by the Supabase validation scheme in hosted-test mode and resolve `sub` only through a database `users.external_subject`; retain cookie sessions for local demo and existing configured auth.
- Add `supplier_applications` states `pending`, `approved`, `rejected`; save business name, contact name, email, phone, city, Supabase subject, timestamps, and review audit fields.
- Add `POST /api/partner/applications` for verified authenticated users; add admin-scoped `GET /api/manage/partner-applications` and `POST /api/manage/partner-applications/{id}/review` with `{ decision, reason }`.
- On approval, atomically create an approved supplier and owner user linked to the application's verified Supabase subject; on rejection, create no supplier/user access. Prevent duplicate pending/approved applications for one identity/email.
- For initial hosted-test administrator setup, provide a one-time documented SQL operation that maps an already-created Supabase Auth user to an admin row; do not expose a public admin bootstrap endpoint or service-role key.

- [ ] **Step 1: Add failing API tests** for valid verified applicant submission, duplicate identity/email, invalid/unverified bearer, pending-user denial, admin-only review, approval mapping, rejection, and cross-supplier isolation.
- [ ] **Step 2: Run** `dotnet test tests/Kosova.Tests/Kosova.Tests.csproj`; confirm the new auth/application cases fail.
- [ ] **Step 3: Add migration 004 and API endpoints** using parameterized SQL, explicit transaction for approval, idempotent review rules, rate limiting, and audit records; HostedTest startup verifies schema version 4 and `btree_gist` without attempting DDL.
- [ ] **Step 4: Add integration tests** proving approved owners can create only their own draft vehicles and cannot publish them; preserve the existing admin publication/document gates.
- [ ] **Step 5: Run** the complete .NET tests and PostgreSQL-backed integration tests; verify migration version 4 is recorded once.

### Task 3: Add partner signup, application, and review UI

**Files:**
- Modify: `apps/web/package.json` and lockfile (Supabase browser auth client)
- Modify: `apps/web/lib/api.ts`
- Modify: `apps/web/lib/i18n.ts`
- Modify: `apps/web/components/Marketplace.tsx`
- Modify: `apps/web/components/Dashboard.tsx`
- Modify: `apps/web/app/globals.css` and focused partner styles if needed
- Test: `apps/web/tests/browser/partner-onboarding.spec.ts`

**Interfaces:**
- Add public navigation link at `/${locale}/partners` and route handling in `Marketplace.tsx`.
- Add verified-email Supabase Auth sign-up/sign-in for partner applicants; use public project URL and anon key only, and attach the resulting access token as a bearer token to same-origin API calls.
- Form fields: company name, contact name, email (from verified identity), phone, and city; successful submission displays a pending-review confirmation.
- Add an administrator application review view with approve/reject decisions and a required reason.
- Preserve English, Albanian, and German copy coverage and blue design tokens; do not add any privileged key to `NEXT_PUBLIC_*` configuration.

- [ ] **Step 1: Add failing browser tests** for navigation, sign-up/application, pending confirmation, invalid form, admin review, and localized pages.
- [ ] **Step 2: Run** `npm --prefix apps/web exec playwright test tests/browser/partner-onboarding.spec.ts`; confirm failures before implementation.
- [ ] **Step 3: Implement the Supabase Auth client, API bearer forwarding, localized application form, and admin review UI.**
- [ ] **Step 4: Run** `npm --prefix apps/web run typecheck` and the focused Playwright test against the local API/web stack.
- [ ] **Step 5: Run** the existing browser journey and security-header suites; verify no client-side API errors or missing labels.

### Task 4: Prepare safe container and Vercel/Supabase deployment configuration

**Files:**
- Modify: `infra/Api.Dockerfile`
- Modify: `infra/compose.yaml`
- Modify: `apps/web/next.config.ts`
- Modify: `.env.example`
- Modify: `docs/DEPLOYMENT.md`
- Create: `infra/render.yaml` (only if supported declarative settings do not require secrets)
- Test: deployment/configuration checks and built artifacts

**Interfaces:**
- API host environment: `RuntimeMode=HostedTest`, `ConnectionStrings__Database`, `WebOrigin`, `Supabase__Url`; no secret values checked into repo.
- Vercel build environment: `API_ORIGIN`, `NEXT_PUBLIC_SUPABASE_URL`, and `NEXT_PUBLIC_SUPABASE_ANON_KEY`; database/password/service-role settings are forbidden from frontend build vars.
- Ensure container listens on the host-provided port (with existing 8080 as local default) and exposes `/api/health`.
- Document which provider settings are safe to commit and which must be entered privately by the user.

- [ ] **Step 1: Add config tests** that reject missing hosted-test DB/API settings and verify the API binds the configured host port.
- [ ] **Step 2: Build the API image** with Docker when a daemon is available; verify it starts with a placeholder connection setting only for build and runs as non-root.
- [ ] **Step 3: Update Vercel rewrite/build settings and local Compose defaults** without embedding private secrets.
- [ ] **Step 4: Run** `npm --prefix apps/web run build`, `npm --prefix apps/web run typecheck`, and .NET tests.
- [ ] **Step 5: Inspect generated Next.js output and repository diff** to confirm no secret or local-only backend origin was bundled.

### Task 5: Connect the user's Supabase project and deploy the existing API

**Files:**
- Modify as needed: `docs/DEPLOYMENT.md`
- No database secret is stored in Git or chat.

**Interfaces:**
- Supabase project: `jsjscgamwpblgzzkftuo`.
- GitHub repository: `Arnis2002/kosova-rent-a-car`.
- API service: existing .NET container from `infra/Api.Dockerfile` on the approved free testing host.
- Vercel site: existing blue production alias; do not delete deployments or change brand/design.

- [ ] **Step 1: User completes provider authorization** for GitHub/Supabase and free API hosting where required; user enters the Supabase pooler connection string only in the API host's private settings.
- [ ] **Step 2: Inspect Supabase schema read-only**; stop and report if existing application tables/data are present rather than resetting them.
- [ ] **Step 3: Apply migrations to Supabase** through the authenticated Supabase migration tool, then verify `schema_versions`, the `btree_gist` availability exclusion constraint, and RLS on all app tables.
- [ ] **Step 4: Create a restricted runtime login role with DML grants and BYPASSRLS (no DDL), enter its session-pooler connection string privately, then deploy the existing API container** in HostedTest mode; provision the first admin by the documented one-time SQL mapping of a Supabase Auth user.
- [ ] **Step 5: Set Vercel public API/Auth variables and redeploy the blue frontend**; verify `/api/health`, search, partner application/review, and supplier draft management over the public domain.
- [ ] **Step 6: Report actual deployment URLs, health checks, free-tier cold-start behavior, and any remaining manual action separately; do not call it production-ready.**

### Task 6: End-to-end and release safety verification

**Files:**
- Modify: `apps/web/tests/browser/partner-onboarding.spec.ts` and any regression tests
- Modify: `docs/DEPLOYMENT.md` only for verified outcomes

- [ ] **Step 1: Run** `dotnet test tests/Kosova.Tests/Kosova.Tests.csproj` and the PostgreSQL-backed integration suite.
- [ ] **Step 2: Run** `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build`, and all relevant Playwright journeys.
- [ ] **Step 3: Run hosted smoke tests** for API health, search, invalid/valid applications, approval, denied cross-supplier access, and owner draft creation.
- [ ] **Step 4: Search repository, build output, and logs for connection strings/service-role credentials; verify none are present.**
- [ ] **Step 5: Summarize passed, blocked, and not-yet-verified items; retain the blue Vercel design and label the deployment as test-only.**
