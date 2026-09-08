# Implementation progress

Working brand: Kosova Rent-A-Car. Scope: professional suppliers, exact vehicles and request-to-book. Photo areas intentionally remain empty until real inventory photos are uploaded and approved.

## Verified milestones

- [x] M1 Architecture, domain model, launch assumptions and modular project structure.
- [x] M2 Responsive customer search, results, vehicle detail, extras, checkout and private guest booking access.
- [x] M3 Supplier and administrator workspaces, role checks, supplier isolation, fleet management and document approval.
- [x] M4 PostgreSQL pricing, immutable quotes, availability rules and a database-enforced overlap constraint.
- [x] M5 Request-to-book workflow, supplier acceptance, cancellation, guest access and durable notifications.
- [x] M6 Test payments, pickup/return/no-show operations, support, incidents, replacement consent, reviews and audit history.
- [x] M7 English, Albanian and German routes; responsive browser coverage; keyboard and automated accessibility checks; concurrency, failure and backup/restore verification.
- [x] M8 Container definitions, CI workflow, deployment guide, operations runbook and launch checklist.

## Latest verification results

- ASP.NET Core release build: passed with zero warnings and zero errors.
- Next.js production build and TypeScript validation: passed.
- Business-rule tests: 13 of 13 passed.
- PostgreSQL/API integration tests: 17 of 17 passed, including concurrent acceptance, immutable prices, payment-event idempotency and booking expiry.
- Browser journeys: 5 of 5 passed in installed Chrome, including the full request/accept/cancel flow, all three mobile languages, keyboard navigation, horizontal-overflow checks and WCAG A/AA automated scans.
- Backup/restore drill: passed; row counts and the GiST exclusion constraint were preserved.
- Package installation reported zero known npm vulnerabilities.

## Demonstration-only behavior

Development accounts, seeded suppliers and vehicles, test payment events and local notification delivery exist only when demo mode is explicitly enabled. They are blocked by production startup checks.

## Implemented but not externally verified

The repository includes configurable adapters for OIDC authorization-code login with PKCE and an MFA claim, private S3-compatible storage, ClamAV scanning and SMTP delivery. These require real provider accounts and credentials before their live behavior can be verified. Container configuration was authored but not executed because the local Docker daemon was unavailable.

## External launch blockers

Before a public launch, provide approved legal and tax text, professionally reviewed translations, supplier verification criteria, real supplier and vehicle records, exact-vehicle photos, production identity and MFA configuration, an eligible payment provider, email delivery credentials, private object storage, malware scanning, hosting, backups and production secrets. No public deployment has been performed.
