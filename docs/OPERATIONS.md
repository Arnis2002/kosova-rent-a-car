# Supplier and administrator operating guide

## Development access

Open `/en/login`, `/sq/login`, or `/de/login`. Local accounts and the generated password location are listed in README. Production uses the configured identity provider and requires the configured MFA claim. Accounts are provisioned explicitly; a supplier cannot self-assign another supplier or administrator role.

## Onboard a rental business

1. Administrator: Business → Add rental company. Record the reason.
2. Administrator: Team access → Add team member. Choose the company and owner/operator role. In production provide the exact OIDC subject; development uses a temporary password.
3. Supplier owner: complete business description, pickup locations, operating hours, operating weekdays and response window. Pickup/return outside hours must be explicitly enabled.
4. Supplier: Documents → upload business verification material. Files are quarantined, size/type checked and scanned in configured production. In demonstration, approval is explicitly unscanned and must never be confused with a clean scan.
5. Administrator: review business evidence and approve the supplier. Verification is separate from every vehicle's publication decision. Record the evidence/reason.

## Add and publish an exact vehicle

1. Fleet & rates → Add vehicle. Enter specifications, locations, prices, deposit, driver requirements, cancellation terms, pickup/return instructions and insurance exclusions. Never infer cross-border permissions or equipment.
2. Configure integer-cent prices through the euro inputs. Specify seasonal boundaries (split seasons crossing New Year), duration discount, buffer, fees, extras and tax basis points. Obtain appropriate advice before production tax configuration.
3. Documents → choose the vehicle, upload registration and insurance, and set expiry dates. Photos use kind Photo and PNG/JPEG. Do not upload customer identity documents.
4. Administrator: review documents. Production approval requires a clean scanner result; rejected/unscanned documents cannot publish a real vehicle.
5. Select the approved photo using its Photo action. Photos are stored privately and served only while the associated vehicle and supplier are published/approved. Repeating the action changes the cover photo.
6. Supplier: submit vehicle for review. Administrator: publish after checking all conditions. Missing or expired registration/insurance prevents publication. New bookings also require documents covering the entire booked interval.
7. Material vehicle/condition edits by a supplier return a published vehicle to review. Rate edits affect new quotes only. Existing accepted quotes remain immutable.

## Requests, calendar and handover

- Overview prioritizes awaiting-supplier requests, confirmed pickups and document actions.
- Reservations opens each request, showing the saved price and response deadline. Acceptance attempts a database-protected allocation. A conflicting acceptance fails; do not promise the car before the interface confirms success.
- Pay-at-pickup acceptance confirms. Test prepayment acceptance creates a 20-minute hold and awaits payment. No instant booking exists.
- Availability records external bookings, maintenance and manual blackouts. The same database overlap constraint applies to all allocations. Remove manual records with a reason; manage booking allocations through reservation actions.
- Record pickup within two hours before the pickup time or later. Record return to complete the rental. No-show is available only after pickup time. Reasons are required and handover actions are saved.
- Expiry jobs run every five seconds. When workers were stopped, startup resumes overdue work. Availability remains conservative until expiry is processed.

## Cancellations, refunds and changes

- Guests cancel through their private booking link. The reservation and payment/refund states are separate.
- Paid cancellation requests a refund. Administrator issues the isolated test refund with a reason; retries cannot duplicate it. Late successful payment after expiry also requires a refund.
- Replacement proposals support confirmed, unpaid, same-supplier bookings at the original rental price. Select a different published vehicle. The customer sees its conditions and deposit and must explicitly accept through their private link. The original allocation remains until acceptance; the swap is atomic and may fail if the replacement becomes unavailable.
- Paid replacements, cross-supplier transfers and price-changing replacements require a provider-specific settlement workflow and are deliberately rejected. Arrange cancellation/refund and a new request instead.
- Support & incidents records cases and operational descriptions. Staff resolve ordinary cases without database edits. A generic replacement case never silently changes a booking.

## Reviews and finance

- Only the guest for a completed booking can submit one review. Administrator publishes/rejects it in Support & incidents. Supplier staff cannot moderate reviews. Public ratings appear only from published reviews tied to completed bookings.
- Commission records are created at rental completion from the quote's snapshotted commission rate. Reconciliation marks each record with an audit reason. Rental charge, commission and refundable security deposit remain distinct.
- Platform Audit history lists actor, reason, timestamp and before/after values. Sensitive guest tokens and passwords are not included.
- Team access → Revoke access invalidates sessions and login mappings. Self-revocation is blocked.

## Notifications and privacy

- Development notifications appear as `development_mailbox`; no email is sent. Customer pages show localized status events.
- Production SMTP requires STARTTLS and configured credentials. Delivery is durable and at least once with a stable Message-ID; an SMTP acknowledgement lost after delivery can produce duplicate email. Payment processing is independently idempotent.
- Failed jobs back off, then enter a failed state after eight attempts. Administrator → Notifications → Retry delivery after correcting the delivery issue.
- Guest access links are bearer secrets, hashed for lookup and protected with Data Protection for delivery. URL fragments are removed from the address bar after access and never sent as request URLs. The token is stored in session storage and sent only in the access header. Access expires after return plus configured retention days.
- Customer contact and encrypted link material are removed for terminal bookings after access expiry, except unresolved refunds. Broader document/support/legal-retention schedules require approval before launch. Protect and back up the key ring; losing keys prevents notification links from being decrypted.
