# Architecture and decisions

Next.js 16 / React / TypeScript frontend, ASP.NET Core 10 modular API, PostgreSQL 17 database. Browser calls same-origin /api through Next.js rewrites. No Redis. PostgreSQL provides durable jobs and booking exclusion constraints.

## Invariants
Money is integer EUR minor units; deposits are separate. Half-open UTC inventory intervals [start,end), including configured cleaning buffer. Rental days are ceil(elapsed hours / 24); no grace period initially. Europe/Belgrade is the explicit operating timezone for Kosovo. Local ambiguous/nonexistent DST times are rejected; clients supply offset-qualified instants. Seasonal rates follow local pickup-day boundaries per 24-hour billed block. Default tax is unconfigured (zero in demonstration only), not a statement of legal tax treatment.

Pending requests do not reserve inventory. Acceptance transaction creates an allocation protected by GiST exclusion. Pay-at-pickup confirms immediately; test prepayment creates a timed hold, then payment confirms it. Expiry and terminal transitions release allocations. Quote contents are immutable snapshots, valid for 15 minutes. Supplier acceptance never reprices an existing booking.

Supplier approval and vehicle publication are separate. Publication requires approved, unexpired registration and insurance documents. Search and acceptance recheck validity through the rental end. Uploaded sensitive files remain quarantined until reviewed/scanned. No identity documents collected from guests.

Roles: admin; supplier owner/operator scoped by supplier membership. Cookie sessions are HTTP-only, same-site strict, secure outside development. Privileged production authentication requires external OIDC with MFA before launch. Local accounts are development-only and provisioned explicitly.

## Booking transitions
awaiting_supplier -> confirmed | awaiting_payment | declined | expired | cancelled
awaiting_payment -> confirmed | expired | cancelled
confirmed -> active | cancelled | no_show
active -> completed
Terminal statuses have no normal outgoing transitions. Payment/refund/dispute states are independent. Every mutation is audited with actor, timestamp, reason and snapshots. Replacement proposals require explicit customer acceptance before any material change.

## Model
Suppliers have memberships, locations, operating hours, documents and vehicles. Vehicles have documents, rate configuration and allocations. Quotes snapshot vehicle, policy and price lines. Bookings reference quotes and own history, allocations, payment records, handover records, incidents and commissions. Support cases, reviews, audit events and notification jobs are durable database records.

## Sources
https://nextjs.org/docs/app/getting-started/installation
https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
https://www.postgresql.org/docs/17/rangetypes.html

## Decisions and conflicts
No master-plan document supplied. No conflicts identified. No private owners or instant booking. All seeded businesses, vehicles and policy text explicitly demonstration-only. Production startup rejects demo payment and seed settings. Minimum launch approval and real service configuration remain external dependencies.
