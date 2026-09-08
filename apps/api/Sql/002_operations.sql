ALTER TABLE users ADD COLUMN external_subject text UNIQUE;
ALTER TABLE bookings ADD COLUMN access_secret text;
ALTER TABLE bookings ADD COLUMN access_expires_at timestamptz NOT NULL DEFAULT (now()+interval '1 year');
ALTER TABLE documents ADD COLUMN scan_state text NOT NULL DEFAULT 'pending';
CREATE INDEX notification_pending ON notification_jobs(available_at) WHERE state='pending';
CREATE INDEX booking_expiry ON bookings(response_due,payment_due) WHERE status IN ('awaiting_supplier','awaiting_payment');
CREATE INDEX documents_vehicle ON documents(vehicle_id,kind,expires_on);
CREATE UNIQUE INDEX one_review_per_booking ON operations(booking_id) WHERE kind='review';
INSERT INTO schema_versions(version) VALUES (2);
