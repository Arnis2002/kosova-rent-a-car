BEGIN;
ALTER TABLE allocations ADD COLUMN scheduled_end timestamptz;
ALTER TABLE allocations ADD COLUMN buffer_minutes integer NOT NULL DEFAULT 0 CHECK(buffer_minutes BETWEEN 0 AND 1440);
ALTER TABLE allocations ADD COLUMN revision integer NOT NULL DEFAULT 1;
-- Preserve legacy occupied ranges exactly. Their original return/buffer split was not saved.
-- New manual periods store both, so editing never adds turnaround a second time.
ALTER TABLE allocations ADD CONSTRAINT allocation_scheduled_end CHECK(scheduled_end IS NULL OR (scheduled_end > lower(period) AND scheduled_end <= upper(period)));
INSERT INTO schema_versions(version) VALUES(3);
COMMIT;
