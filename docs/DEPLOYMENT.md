# Deployment and rollback

## Current local setup

Portable PostgreSQL 17 is bound to 127.0.0.1:55432. The preview database is `kosova`; integration tests use `kosova_test`. API: 5080; integration API: 5081; Next.js: 3000. Local passwords, document storage, key material and database files stay under ignored `.local/`. Start API with `scripts/start-api.ps1`; start frontend separately with `npm run dev --prefix apps/web`.

`infra/compose.yaml` is an alternative private demonstration stack. Supply POSTGRES_PASSWORD, DEMO_PASSWORD and TEST_PAYMENT_SECRET. It binds host ports only to loopback. The Dockerfiles and Compose configuration are provided, but container execution must be verified on a host with a working Docker daemon.

## Staging

Use a private environment behind access control and HTTPS. Keep demonstration and real supplier databases separate. Do not point a demo app at a production database. For a real-service staging rehearsal, set DemoMode=false and provide the validated OIDC, S3, scanner, SMTP, certificate/key-ring, retention and approved terms configuration in `.env.example`.

Production startup rejects demo mode, mixed demonstration inventory, missing security/service configuration and HTTP public origins. Live prepayment is disabled until an eligible provider is selected and implemented. Pay-at-pickup does not require a live card provider.

Run migrations with `dotnet Kosova.Api.dll --migrate-only` using a migration credential before starting application instances. The current startup also checks/applies migrations under a database advisory lock; deployment should use a credential with only required schema permissions and separate production migration ownership before exposure. Back up first. SQL files are ordered in `apps/api/Sql` and tracked in schema_versions.

Use an HTTPS reverse proxy. Expose the frontend only; keep API/PostgreSQL/scanner private. Configure WebOrigin to the exact public origin and API_ORIGIN to the internal API service. Web build embeds the API rewrite origin; rebuild when it changes. Keep private object buckets blocked from public access and grant only the required object-prefix permissions using workload identity. Never mount private documents beneath the web public directory.

Persist Data Protection keys in encrypted protected storage, including the certificate used to protect them. Set the OIDC callback to `<WebOrigin>/api/auth/callback`. Provision the first admin using the exact subject and email; remove bootstrap values after successful provisioning. Require MFA in the identity provider and verify its exact claim/value mapping.

## Release checklist

1. Run unit, integration, concurrency, build, browser and accessibility checks.
2. Run database backup/restore drill; test recovery of both DB and object/key material.
3. Rehearse OIDC MFA and account isolation with actual staging identities.
4. Rehearse infected/invalid upload rejection, private access denial and real SMTP retry/dead-letter handling.
5. Confirm reviewed policy versions, translations, tax configuration, actual fleet documents and contact details.
6. Publication authorization was received for the frontend preview on 2026-09-09. Obtain separate approval after all remaining production dependencies are ready before enabling the live API and accepting real bookings.
7. Deploy immutable versioned images, migrate, check health and perform a smoke booking with test records only. Monitor failure rates, expiry backlog, notification failures, document expiries and unresolved refunds.

## Backup and restoration

`scripts/backup-restore.ps1` dumps the isolated test database, restores into a uniquely named new database, compares critical table counts, and verifies the allocation exclusion constraint exists. It never overwrites the live or preview database. The restored database is deliberately retained for inspection. Backups may contain personal data; keep artifacts private and do not publish them as public build artifacts.

Production: schedule encrypted PostgreSQL backups and point-in-time recovery, S3 versioning and lifecycle rules, and key/certificate backups. Define and approve RPO/RTO and retention. Rehearse restoring all three together before launch; a database-only restore does not verify object or key recovery.

## Rollback

Record release image IDs and the migration version. Prefer rolling application code back only when it remains compatible with the current schema. Migrations are additive in this initial release; do not manually delete columns or quote records. For incompatible schema rollback: stop writes and workers, restore the verified backup into a new database, restore matching objects and keys, switch connections, then validate bookings/allocations/payment reconciliation before reopening. Never replay live payment charges during restoration; reconcile by provider IDs and webhook event IDs.
