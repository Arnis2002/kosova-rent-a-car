# Rent Kosova Brand Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the working Kosova Rent-A-Car identity with a complete, coherent Rent Kosova brand across the product and repository.

**Architecture:** Centralize visible identity in reusable SVG assets and a small React brand component, then apply semantic design tokens globally. Protect the rebrand with a file-level Node test that checks names, metadata, assets, localization, and removal of the former public name.

**Tech Stack:** Next.js 16, React 19, TypeScript, CSS, SVG, Node test runner

**Spec:** `docs/superpowers/specs/2026-09-19-rent-kosova-brand-design.md`

## Global Constraints

- Public name is exactly `Rent Kosova`.
- Brand personality is modern local confidence.
- Preserve exact-vehicle inventory, request-to-book behavior, separate deposits, truthful prices, and demonstration labels.
- Do not invent suppliers, testimonials, certifications, guarantees, or operating claims.
- Preserve `/en`, `/sq`, and `/de`, responsive behavior, reduced motion, and WCAG AA.
- Do not create a commit, push, or deployment as part of this plan.

---

### Task 1: Brand contract and identity assets

**Files:**
- Create: `apps/web/tests/brand.test.mjs`
- Create: `apps/web/public/brand/rent-kosova-mark.svg`
- Create: `apps/web/public/brand/rent-kosova-logo.svg`
- Create: `apps/web/public/brand/rent-kosova-logo-reversed.svg`
- Create: `apps/web/app/icon.svg`

**Interfaces:**
- Produces: stable `/brand/*.svg` assets and file-level assertions consumed by the remaining tasks.

- [ ] Write the Node test asserting exact public name, SVG asset presence, localized brand lines, metadata, manifest, and absence of the former public name from product source.
- [ ] Run `node --test tests/brand.test.mjs` from `apps/web` and confirm it fails because the new identity is absent.
- [ ] Create the accessible SVG mark and horizontal lockups, using Road Navy, Kosova Blue, and Sun Gold.
- [ ] Add the Next.js icon asset derived from the square mark.
- [ ] Re-run the brand test after Tasks 2 and 3 and confirm it passes.

### Task 2: Product identity and localized voice

**Files:**
- Create: `apps/web/components/Brand.tsx`
- Modify: `apps/web/components/Marketplace.tsx`
- Modify: `apps/web/lib/i18n.ts`
- Modify: `apps/web/app/layout.tsx`
- Create: `apps/web/app/manifest.ts`

**Interfaces:**
- Produces: `Brand({compact?: boolean, inverse?: boolean})` and localized `brandLine` dictionary entries.

- [ ] Implement the brand component from the approved SVG mark geometry with an accessible wordmark.
- [ ] Replace header, footer, page eyebrow, and team workspace references with the component or exact name.
- [ ] Add the three approved brand lines and use them in the public experience without weakening demo disclosures.
- [ ] Update metadata, Open Graph information, application name, icons, and installable manifest.
- [ ] Run type checking and correct any TypeScript errors.

### Task 3: Visual system and repository identity

**Files:**
- Modify: `apps/web/app/globals.css`
- Modify: `package.json`
- Modify: `apps/web/package.json`
- Modify: `README.md`
- Modify: `docs/PROGRESS.md`
- Create: `docs/BRAND.md`
- Modify: `apps/api/Hosting.cs`

**Interfaces:**
- Consumes: Brand component class names and approved design tokens.
- Produces: consistent brand styling and durable usage guidance.

- [ ] Replace legacy green tokens with the approved navy, blue, gold, mist, and warm-road palette while preserving semantic status colors.
- [ ] Style the new mark and wordmark across desktop, mobile, footer, and workspace contexts.
- [ ] Rename package/application identifiers where this does not break external infrastructure.
- [ ] Write the brand guide with positioning, voice, naming, visual rules, asset inventory, accessibility, and prohibited claims.
- [ ] Update documentation to distinguish the Rent Kosova brand from legacy preview URLs that cannot be renamed locally.

### Task 4: Regression verification

**Files:**
- Modify: `apps/web/tests/browser/journeys.spec.ts`

**Interfaces:**
- Consumes: visible Rent Kosova navigation identity and unchanged marketplace flows.

- [ ] Add a browser assertion for the Rent Kosova home-link accessible name.
- [ ] Run `node --test tests/brand.test.mjs` from `apps/web`.
- [ ] Run `npm run typecheck --prefix apps/web` from the repository root.
- [ ] Run `npm run build --prefix apps/web` from the repository root.
- [ ] Run the available browser smoke tests if the local API and database are running; otherwise report that dependency precisely.
- [ ] Inspect `git diff`, search for legacy visible names, and reconcile every change against the specification.
