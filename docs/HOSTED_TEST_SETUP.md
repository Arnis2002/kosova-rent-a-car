# Hosted test setup

This is a test deployment, not a launch configuration. It has no real payment processing, email delivery, persistent/private document storage, malware scanning, or production backup/SLA guarantee. The API intentionally rejects private document uploads and suppresses outbound notifications in `HostedTest` mode.

## Supabase

The Supabase project `jsjscgamwpblgzzkftuo` has the app schema installed through migration 006. The app tables have RLS enabled and no browser-accessible policies. The website uses Supabase Auth only; application data goes through the .NET API.

1. In Supabase Auth settings, enable email/password signup, email confirmation, and add the exact Vercel site URL plus `https://<your-vercel-domain>/en/partners` as allowed redirect URLs.
2. In the SQL editor, create a dedicated API login role. Generate a unique password in a password manager and substitute it privately; never commit it or paste it into chat:

```sql
CREATE ROLE rent_kosova_api LOGIN PASSWORD '<generate-a-unique-secret>'
  NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS;
GRANT CONNECT ON DATABASE postgres TO rent_kosova_api;
GRANT USAGE ON SCHEMA public TO rent_kosova_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO rent_kosova_api;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO rent_kosova_api;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO rent_kosova_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO rent_kosova_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO rent_kosova_api;
```

Copy the Supabase Session pooler URI from **Connect** and replace its username/password with this role and your private password. Keep SSL required. Add it only to the API host's secret environment setting `ConnectionStrings__Database`. Do not use the database owner password for the running API.

3. Create the platform administrator in Supabase Auth (email confirmed), then provision that user using the Auth user's UUID in SQL editor:

```sql
INSERT INTO public.users(id,email,password_hash,role,external_subject)
VALUES ('<auth-user-uuid>','<admin-email>','supabase-auth-only','admin','<auth-user-uuid>');
```

Do not expose the UUID/password credentials publicly. Admin enrollment is not open from the public partner page.

## API host (Render)

Create a Web Service from the GitHub repository using the included `render.yaml`. Add these private values in the service environment:

- `WebOrigin`: exact deployed Vercel HTTPS origin (no trailing slash).
- `ConnectionStrings__Database`: Supabase Session pooler connection string using the restricted role above.
- `Supabase__PublishableKey`: enabled Supabase publishable key (not a secret/service-role key).

Wait for `/api/health` to return HTTP 200 before wiring Vercel. Free services may sleep when idle; first request can be slow.

## Vercel

Set these environment values for Preview (and Production only if intentionally testing there), then redeploy the blue Rent Kosova project:

- `API_ORIGIN`: Render API origin, e.g. `https://rent-kosova-api.onrender.com`.
- `NEXT_PUBLIC_SUPABASE_URL`: `https://jsjscgamwpblgzzkftuo.supabase.co`.
- `NEXT_PUBLIC_SUPABASE_ANON_KEY`: enabled `sb_publishable_...` key from Supabase.

The API's `WebOrigin` must be the same Vercel origin. Supabase redirect allow-list entries, browser Content-Security-Policy, API origin checks, and Vercel rewrite must all match these URLs.

## Verify before inviting rental companies

- Check `/api/health`, `/api/auth/config`, and `/api/locations` on the deployed site.
- Sign up on `/en/partners`, confirm the email, submit a company application, and confirm it appears in the admin's Partner Applications tab.
- Approve a test company, sign in as that owner, and verify a new car stays a draft until the existing vehicle review requirements are met.
- Confirm `supplier_applications` and all app tables have RLS enabled and no policies; never expose the database connection string or privileged keys to the web app.
