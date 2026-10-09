# Supabase Test Deployment and Rental Partner Onboarding

## Goal

Connect the blue Rent Kosova frontend to the existing .NET API and a Supabase PostgreSQL database for free-tier testing, and give prospective rental companies a clear, safe path to join and list cars after review.

## Agreed direction

- Keep the blue Rent Kosova Vercel frontend as the customer-facing design.
- Use the user's Supabase project `jsjscgamwpblgzzkftuo` for PostgreSQL.
- Host the existing .NET API as a separate container web service on a free testing tier (Render is the current proposal); Vercel remains frontend-only.
- Keep database connection details only in the API host's private environment variables.
- Add a public rental-partner application path. Applications are reviewed before a supplier account is activated; an approved supplier can add draft cars, while publication remains subject to the existing administrative/document checks.
- Use test data only. This is not a production launch and must not accept real payments or sensitive identity documents.

## Current project facts

- The API is ASP.NET Core targeting .NET 10 and PostgreSQL through Npgsql.
- SQL migrations are in `apps/api/Sql/001_initial.sql` through `003_availability.sql`; API startup applies them while holding PostgreSQL advisory lock `884021`.
- The initial schema already supports `pending`/`approved`/`suspended` suppliers, `owner`/`operator` roles, draft/submitted/published cars, and administrative review.
- The API currently rejects public demo mode outside Development. Non-demo mode requires a complete production OIDC, S3, scanner, SMTP, and key-protection configuration, so neither mode is appropriate unchanged for a public free test service.
- `apps/web/next.config.ts` embeds `API_ORIGIN` as a rewrite destination at build time. The Vercel project currently has no environment variables, so its API route cannot reach the .NET service.
- The supplied Supabase dashboard screenshot reports a healthy project, no migrations, and no GitHub repository connected. The user confirmed the intended repository is `Arnis2002/kosova-rent-a-car`; linking the GitHub repository in Supabase is optional and is not itself the database connection.

## Proposed architecture

### Runtime and data

1. Vercel serves the existing blue frontend. Its build receives the public API origin as `API_ORIGIN`, making same-origin `/api/*` requests rewrite to the API host.
2. A separate container service runs the existing .NET API. A dedicated, explicitly named hosted-test mode will be added; it will not set ASP.NET's `Development` environment in public hosting and will not masquerade as production.
3. The API connects to the user's Supabase PostgreSQL project using a private session-pooler connection string (or a verified compatible direct connection). Session pooling is required because startup uses a session-level advisory lock. Use a least-privilege application role where practical, and keep migration/app startup permissions and connection setup documented.
4. Apply the existing migrations to the selected Supabase project only after checking that the target schema is empty or recording and preserving any existing data. Do not reset or delete the project.
5. Configure the API's exact public `WebOrigin` to the blue Vercel domain and confirm cookie/origin behavior through the same-origin rewrite. Provide a health endpoint smoke test that confirms API and database connectivity.

### Rental partner membership

1. Add a visible “For rental companies” / “Become a rental partner” entry in the public navigation and a localized application page matching the blue design.
2. The application form collects only business/contact details needed for review and displays a clear pending-review acknowledgment. It must validate and rate-limit submissions and avoid exposing internal admin data.
3. Store an application separately from active suppliers (or add an equivalent explicit application state) so a public form cannot directly create an approved supplier or privileged user.
4. Add an administrator review action. Approval provisions/associates a supplier and owner account through the selected account/auth mechanism; rejection leaves an auditable state and does not create usable access.
5. Once approved and signed in, the owner can create/edit draft cars scoped to their supplier. Existing document and administrative checks continue to gate publication. Public search returns only published cars belonging to approved suppliers.

### Account security decision to implement

Use Supabase Auth for partner credentials and verified email, while the API remains authoritative for supplier membership and access to management endpoints. The implementation must validate Supabase-issued tokens server-side, bind the verified identity to a locally approved `users` row, and preserve existing supplier scoping. Never accept a client-supplied supplier ID as proof of access. If the existing authentication/session model cannot safely support this without broad replacement, stop and present the smallest revised design before expanding scope.

### Test-only safeguards

- Hosted test mode is allowed only with an explicit environment setting, HTTPS `WebOrigin`, and a database name/project identity explicitly labeled for testing.
- Do not enable real payment capture, customer email sending, public document storage, or production OIDC/admin bootstrap as a side effect of test mode.
- Do not use local/ephemeral container disk for documents or keys and imply persistence. For this phase, disable partner document upload if no private persistent object storage/scanner is configured; retain safe draft creation and test-only listing flow.
- Mark demo/test inventory and customer-visible test limitations clearly. Seed only fictional records.
- Supabase connection credentials and privileged auth keys stay only in the API host's protected settings. A public Supabase anon key, if needed by the browser auth flow, is not a database credential; database/service-role secrets are never exposed in browser bundles.

## Alternatives considered

1. Supabase PostgreSQL + separate free container API + Vercel frontend (recommended): fits the existing .NET API and schema, keeps secrets server-side, but free API sleep/cold starts and free-tier limits make it test-only.
2. Move the .NET API into Vercel functions: avoids a separate API host but requires adapting a long-running .NET API/background worker and database connection lifecycle; higher risk and not the smallest integration.
3. Keep everything local with Docker/PostgreSQL: lowest deployment risk but does not test the hosted Vercel frontend against a public API/database.

## Acceptance criteria

- The Vercel blue site loads and `/api/health` reports a healthy API/database response through same-origin routing.
- Existing migrations apply once to the intended Supabase test database without dropping data; the required `btree_gist` availability constraint works.
- Search reads seeded test cars from Supabase and respects availability and published/approved filters.
- A visitor can find and submit a rental-partner application; duplicate/invalid/rate-limited submissions are handled safely.
- A pending applicant cannot log in to supplier management or publish a car. An approved owner can sign in and manage only their supplier's draft inventory; publication remains review-gated.
- No database password or privileged Supabase key appears in frontend output, Git history, logs, or checked-in env files.
- Automated API/web tests, production builds, and hosted smoke tests pass; limitations and any remaining external setup are reported explicitly.

## Out of scope

- Real rental transactions, real payment capture, production launch, production-grade backups/SLA, public document uploads without private persistent storage and scanning, DNS/domain changes, and deleting any Vercel deployment or Supabase project.

## Open deployment prerequisite

The user has identified the Supabase project and GitHub repository, but the project dashboard screenshot does not provide a database connection string or show a GitHub repository linked. The user must authorize the GitHub repository inside Supabase if desired and enter the database connection string in the API host's private settings. Secrets must not be sent through chat. Free API hosting account/project creation and authentication may also require the user to complete the provider sign-in flow.
