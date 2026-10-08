# Rent Kosova Release Readiness Implementation Plan

> **For agentic workers:** Use the native implementation workflow task by task. Verify each task before moving to release actions.

**Goal:** Repair the visible development CSP error, validate the complete Rent Kosova frontend and API project, and prepare and publish a verified Vercel preview from the Arnis2002 GitHub repository.

**Architecture:** Keep the stricter Content Security Policy in production while allowing the development-only React runtime requirement. Preserve the ASP.NET/PostgreSQL boundary and treat Vercel as the frontend preview unless a separate production API host is configured.

**Tech Stack:** Next.js 16, React 19, Playwright, ASP.NET Core 10, PostgreSQL 17, GitHub, Vercel.

**Spec:** Approved in-chat design on 2026-10-08.

## Global Constraints

- Preserve the user's existing local changes and include intended branding and availability work in the release.
- Keep local secrets, database files, build output, and private runtime files out of Git.
- Do not represent the Vercel frontend preview as production-ready full-stack hosting.
- Verify frontend, API, database integration, browser journeys, and public deployment before reporting status.

## Review Focus

- Development CSP must permit the React debugging runtime; production CSP must continue to omit `unsafe-eval`.
- Supplier availability changes and migration must be exercised against PostgreSQL.
- Production environment and test fixture secrets must not expose local credentials.
- Browser tests must detect runtime errors and preserve accessibility and responsive flows.
- The published Vercel deployment must be checked separately from local build success.

---

### Task 1: Reproduce and fix development CSP failure

**Files:**
- Modify: `apps/web/next.config.ts`
- Create: `apps/web/tests/browser/security-headers.spec.ts`

- [ ] Add a browser assertion that checks `unsafe-eval` is present only when the development expectation is explicitly enabled.
- [ ] Run the assertion against the current development server and confirm it fails because the policy lacks `unsafe-eval`.
- [ ] Add `unsafe-eval` to `script-src` only when `NODE_ENV` is `development`.
- [ ] Run the browser assertion in development and production modes; confirm development includes it and production omits it.

### Task 2: Validate marketplace and release hygiene

**Files:**
- Review all changed and untracked application, test, documentation, and configuration files.
- Modify only files with confirmed defects.

- [ ] Run frontend typecheck and production build.
- [ ] Run .NET business-rule tests and API/PostgreSQL integration suites.
- [ ] Run browser journeys, availability flows, and accessibility assertions.
- [ ] Review tracked changes for secrets, runtime artifacts, accidental generated files, and environment mismatches.
- [ ] Fix confirmed failures and repeat affected regression checks.

### Task 3: Review and publish

**Files:**
- All approved project source and documentation changes.

- [ ] Obtain a fresh review of the final diff and resolve important findings.
- [ ] Commit the approved project changes on `main`.
- [ ] Push to `Arnis2002/kosova-rent-a-car` and verify the remote SHA.
- [ ] Deploy to the existing Vercel project and verify public rendering and security headers.
- [ ] Report preview scope and any full-stack work blocked on a separately hosted API/database.
