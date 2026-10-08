# Rent Kosova Brand Guide

## Core idea

Rent Kosova helps people make clear car-rental choices for journeys in Kosovo. The brand combines local familiarity with the operational confidence expected from a managed marketplace.

**Promise:** Clear choices for the road ahead.

**Descriptor:** Car rental marketplace in Kosovo.

**Personality:** Confident, welcoming, direct, calm.

## Name

Always write the public name as **Rent Kosova**. Both words use title case. Do not contract it to `RentKosova`, translate Kosova to Kosovo in the name, or use the former public name in product copy.

The public name stays the same in Albanian, English, and German. Localized brand lines are:

- Albanian: `Zgjedhje të qarta për rrugën përpara.`
- English: `Clear choices for the road ahead.`
- German: `Klare Auswahl für die Straße vor Ihnen.`

## Logo

The mark combines `R` and `K` into a compact route monogram. Its gold rising cut suggests a route opening forward. It intentionally avoids a generic car silhouette so the identity can represent the full marketplace, including customers, rental companies, and operations.

Assets:

- `apps/web/public/brand/rent-kosova-mark.svg`: standalone mark for avatars and compact surfaces.
- `apps/web/public/brand/rent-kosova-logo.svg`: primary horizontal logo for light surfaces.
- `apps/web/public/brand/rent-kosova-logo-reversed.svg`: horizontal logo for dark surfaces.
- `apps/web/app/icon.svg`: browser and installable-app icon.

Keep clear space around the mark equal to at least one quarter of its width. Do not recolor individual letterforms, stretch the artwork, place it on a low-contrast image, add effects, or recreate the mark with another typeface.

## Color

| Role | Name | Hex | Use |
| --- | --- | --- | --- |
| Text | Road Navy | `#132A3A` | Headlines, body text, dark surfaces |
| Brand | Kosova Blue | `#2457D6` | Primary actions, links, mark field |
| Accent | Sun Gold | `#F2B544` | Small highlights only |
| Tint | Mountain Mist | `#EAF0F5` | Informational and selected surfaces |
| Canvas | Warm Road | `#F7F8F5` | Page background |
| Surface | White | `#FFFFFF` | Cards, forms, navigation |
| Danger | Signal Red | `#A33A2B` | Destructive actions only |

Gold is not used for body copy on light backgrounds. Status colors retain their semantic meaning and are not replaced solely for brand consistency.

## Typography and shape

The interface uses `Avenir Next`, then `Segoe UI`, Arial, and sans-serif fallbacks. This keeps the application fast and avoids a third-party font dependency. Headlines are bold and tightly tracked. Body copy is regular or medium weight with generous line height.

Use the existing 7px to 14px corner-radius family. Buttons remain compact rounded rectangles, not pills. The logo field uses a slightly larger radius so it stays recognizable at small sizes.

## Voice

Write like a knowledgeable local host: useful before clever, specific before promotional.

- Say what happens next.
- Separate the rental total from the refundable deposit.
- Call a request a request until the rental company confirms it.
- Prefer `rental company` in customer copy and `supplier` in operations copy.
- Keep instructions short and active.

Examples:

- Use: `Send booking request`.
- Avoid: `Book instantly`.
- Use: `Supplier confirmation required`.
- Avoid: `Your car is guaranteed`.
- Use: `Compare exact vehicles and total prices`.
- Avoid: `The lowest prices in Kosovo`.

## Claims and safety

Do not claim guaranteed availability, instant confirmation, lowest prices, universal supplier verification, certifications, live payment capability, or production readiness unless each statement is currently supported. Demonstration inventory, test payments, draft legal copy, and unavailable production services must remain explicitly labeled.

## Accessibility

Use the supplied SVG title and description when the asset stands alone. When a linked logo already has the accessible name `Rent Kosova`, treat the nested image as decorative. Maintain visible keyboard focus, WCAG AA contrast, 44px touch targets where practical, and reduced-motion behavior.
