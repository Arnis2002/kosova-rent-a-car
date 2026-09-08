# Launch gate and external dependencies

No public deployment, service purchase, domain registration, supplier contact or real charge has been performed.

## Must be supplied and verified before launch

- Real business/operator identity, support contacts and approved supplier-verification procedure.
- Actual supplier inventory and photos, vehicle registration/insurance evidence and supplier-authored rental conditions.
- Professionally reviewed platform/legal/privacy/cancellation content, jurisdiction-specific tax configuration, and Albanian/German/English translations. Draft pages are not legally approved.
- Identity provider tenant, client credentials, exact MFA claim mapping and production staff subjects. The OIDC adapter is implemented; actual provider login/MFA remains externally unverified.
- Private S3 bucket/region and workload permissions, malware-scanning endpoint with updated signatures, and approved upload/document retention rules. Adapters are implemented; live object storage and scanning remain externally unverified.
- SMTP host, credentials and verified sender/domain. Durable retry processing is implemented. Local delivery is a clearly labeled mailbox; no real email has been sent.
- An eligible payment provider for the registration country, marketplace structure, EUR, payment methods and payouts. No provider eligibility is assumed. Test adapter, signed event verification, retries, refunds and reconciliation are implemented. Live payment/authorization/capture/payout integration remains dependent on provider selection. Production rejects test checkout and unsupported prepayment.
- Hosting, TLS, secrets, backup schedules, RPO/RTO, monitoring and explicit publication approval. Container/cloud deployment and live recovery require staging infrastructure.

## Supported initial operating boundaries

Exact vehicles, professional businesses, two initial locations, configurable daily operating schedule, request-to-book, half-open buffered intervals, EUR, guest access, one supplier membership per account, same-supplier same-price unpaid replacement proposals, one moderated review per completed booking, full test refunds. No category-capacity allocation, instant booking, cross-supplier replacement settlement, real card storage or private-owner rentals.

## Remaining production hardening gates

- Independent security review and manual assistive-technology checks; automated axe checks are not a complete accessibility certification.
- Real-service failover and object/key restoration rehearsal; local PostgreSQL restore is only the database part.
- Define final document/support/audit retention and legal holds. Automated guest-contact removal is configurable; full legal retention policy requires operational approval.
- Test volume and query plans with production-scale inventory; current management views intentionally limit history to 300 records.
- Validate real-provider settlement before enabling live prepayment, partial/multiple refunds, payouts, disputes and paid/price-changing replacements.
- Add approved business-specific policy translations for actual suppliers; unprovided policy translations remain visibly flagged.
