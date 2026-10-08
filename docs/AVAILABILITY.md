# Rental-company fleet and availability

Open `/en/login`, `/sq/login`, or `/de/login` through **For rental companies**. Each company account sees only its own vehicles, documents, bookings and unavailable periods. Platform administrators can manage all companies.

## Add and manage cars

1. Open **Fleet & rates**, then **Add vehicle**. Enter the exact vehicle, pickup locations, prices and rental conditions.
2. Save the car as a draft. Upload its actual photos, registration and insurance through **Documents**. Approved photos can be assigned to the car using the Photo action.
3. Submit the car for review. Publication still requires administrator approval and valid documents.
4. Use the fleet search and listing-status filter to find a car. **Manage availability** opens the calendar filtered to that car.

## Calendar

The 14-day calendar can show every car or a selected vehicle. Move backward or forward by 14 days, choose a starting date, or return to Today. On narrow screens the calendar scrolls horizontally without widening the page.

Select a clear day or choose **Block dates**. Choose external booking, maintenance, or manual block, then enter start, return/end time and a reason. Times are always interpreted in Kosovo local time, including daylight-saving changes, regardless of the user's device timezone.

**Edit dates** changes a manual period atomically. **Release these dates** opens a confirmation form and removes only that period. Existing platform reservations and payment holds can only be changed through Reservations. A day may contain more than one non-overlapping period; the period list shows the individual times.

Calendar colors describe occupied time. “No blocks” does not promise that an unpublished car or a car with expired documents can be booked. Public eligibility also depends on publication, supplier approval, valid documents, location, driver eligibility, rental duration and supplier conditions.

## Customer search

The API excludes cars whose occupied periods overlap any part of the requested interval, including turnaround time. New searches use saved availability immediately. Open search pages refresh every 30 seconds and when the visitor returns to the tab. When refresh fails, outdated results are removed and an error is shown. Quote and booking checks still verify eligibility on the server.

## Data and concurrency

Migration `003_availability.sql` adds the entered end time, saved turnaround minutes and a revision number to allocation records. It is applied automatically at API startup under the existing migration lock. Legacy occupied ranges are preserved exactly; their previously unsaved end/buffer split is not guessed.

The update endpoint requires `expectedRevision`; stale edits return `availability_changed` (409). The UI also sends that revision for removals. Existing callers without a revision retain the previous removal contract. The PostgreSQL exclusion constraint remains the authority for conflicts between bookings, payment holds and manual periods. A rejected edit leaves the original period intact. Every change is recorded in audit history.

## Local startup and checks

Restart the API after pulling these changes so it builds the current code and runs migration 003:

```powershell
cd C:\Users\Admin\Documents\ChatGPT\RentaCar
powershell -ExecutionPolicy Bypass -File .\scripts\start-local.ps1
```

The startup scripts now compile the API instead of silently using an older binary. Do not start a second instance on the same port; stop the previous one with Ctrl+C first.

Availability regression checks use the separate `kosova_test` database and API on port 5081. From `apps/web`, run `node tests/availability.mjs`, then `node tests/integration.mjs`. Browser tests use port 3000 by default; set `TEST_WEB` for a separate web server. They cover adding a car, editing/releasing availability, live customer-result refresh, mobile layouts, accessibility, localization and Kosovo clock-change boundaries.
