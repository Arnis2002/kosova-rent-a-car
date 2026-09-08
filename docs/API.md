# API contracts

All prices are integer EUR minor units. JSON responses use database-style snake_case for stored records and camelCase for request DTOs and immutable quote snapshots. Errors use `{ "error": "stable_code" }` with 400 validation, 401 authentication, 403 resource/role denial, 404 missing, 409 state/overlap conflict, 429 rate limit and 503 unavailable provider. Frontend displays localized customer-safe errors. Clients never submit authoritative totals.

## Public and guest

- `GET /api/health`, `/api/locations`, `/api/auth/config`.
- `POST /api/time`: `{start,end}` in `YYYY-MM-DDTHH:mm` Kosovo local time. Rejects ambiguous/nonexistent DST time. Returns UTC instants.
- `POST /api/search`: `{pickup,dropoff,start,end,age,extras:[]}`. Dates are offset-qualified instants. Published/approved inventory, age, operating hours, interval, documents and allocations are considered. Deterministic price + vehicle ID ordering.
- `GET /api/vehicles/{id}`, `/api/suppliers/{id}`: public records only.
- `POST /api/quotes`: search DTO plus `vehicleId`. Returns `{id,expiresAt,snapshot}`. Expires in 15 minutes; snapshot contains all price lines, total, deposit, pay-now/pickup, policies and terms version.
- `POST /api/bookings`: `{quoteId,locale,acceptTerms,termsVersion,customer:{name,email,phone,licenceYears}}`. Returns one-time `{id,accessToken,status,responseDue}`. A unique quote creates at most one booking. Do not log this response.
- `GET /api/bookings/{id}` requires session scope or `X-Booking-Token`. Returns immutable current snapshot, status, histories, proposals, payment records and notification delivery states. Access secrets are omitted.
- `POST /api/bookings/{id}/transition`: `{status,reason,record?}`. Guest can only cancel; supplier/admin transitions obey lifecycle rules.
- `POST /api/bookings/{id}/payment`: creates/reuses an isolated test checkout. `POST /api/test-payments/{paymentId}/complete` accepts `{state:'succeeded'|'failed'}` in demo only.
- `POST /api/payments/test/webhook`: signed raw JSON `{eventId,paymentId,state}`, HMAC-SHA256 hex in `X-Test-Signature`. Rejects invalid signatures. Duplicate event IDs and out-of-order failures after success do not duplicate payments.
- `POST /api/bookings/{id}/replacement/{proposalId}`: `{accept,termsVersion}`. Valid guest token required even when a supplier session exists.
- `POST /api/bookings/{id}/review`: `{rating:1..5,text}`. Guest + completed booking required; one review per booking.
- `POST /api/support`: `{email,message}` persists an operational case.

## Staff

Development login: `POST /api/auth/login` with `{email,password}`. Production: `GET /api/auth/oidc`, callback `/api/auth/callback`; OIDC authorization code + PKCE, MFA claim required, explicit subject-to-user mapping. `GET /api/auth/me` and `POST /api/auth/logout`.

All `/api/manage/*` routes require a valid staff session. Every supplier resource is scoped to the actor's supplier ID; administrator-only operations enforce the admin role.

- `GET overview`: scoped suppliers, vehicles, bookings, documents, allocations, operations, commissions and future document-review flags.
- `POST suppliers`, `POST suppliers/{id}`: admin creates/approves/suspends; supplier owner maintains profile and operating configuration.
- `GET users`, `POST users`, `POST users/{id}/revoke`: owner/admin membership management. Development password or production `externalSubject`; role restricted to owner/operator. Admin bootstrapping is a separate first-start configuration.
- `POST vehicles`, `POST vehicles/{id}`: `name,supplierId,spec,rate,policy,reason`; draft/submitted/published/withdrawn workflow.
- `POST documents`: multipart `supplierId,vehicleId?,kind,expiresOn,file`; maximum 5 MB; PDF/JPEG/PNG signature checks; photo requires image. Private quarantine and scanner verdict. `GET documents/{id}/download` is authorized and audited. `POST documents/{id}/review` requires administrator and a clean production scan.
- `POST vehicles/{id}/photo`: `{documentId,reason}` selects an approved photo belonging to that exact vehicle. `GET /api/images/{id}` exposes only approved vehicle photos for published vehicles and approved suppliers, never business/insurance/registration documents.
- `POST allocations`: `{vehicleId,start,end,kind,reason}`. `POST allocations/{id}/remove` requires a reason and cannot remove booking allocations.
- `POST bookings/{id}/replacement`: `{vehicleId,reason}` same-supplier, same-price unpaid replacement proposal.
- `POST bookings/{id}/refund`: admin-only isolated test refund for paid cancelled/expired bookings.
- `POST operations`, `POST operations/{id}`: cases and moderation with reasons. Replacement acceptance is a separate guest route.
- `POST commissions/{bookingId}/reconcile`: administrator reconciliation.
- `GET audit`, `GET notifications`, `POST notifications/{id}/retry`: administrator operational history/delivery recovery.

## Protection

Session cookies are HTTP-only, SameSite=Strict, Secure outside local development. Mutation origins are checked against WebOrigin; OIDC callback uses its protocol protections. Request rate limits, request size limits, parameterized SQL and resource checks apply on the API. Guest headers with invalid tokens fail immediately; they never fall back to a staff session for customer-only approval. JSON money is validated/recomputed server-side. PostgreSQL quote triggers prevent mutation and GiST exclusion constraints prevent overlapping allocations.
