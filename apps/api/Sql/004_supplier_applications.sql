BEGIN;
CREATE TABLE supplier_applications(
    id uuid PRIMARY KEY,
    auth_subject text NOT NULL,
    company_name text NOT NULL,
    contact_name text NOT NULL,
    email text NOT NULL,
    phone text NOT NULL,
    city text NOT NULL,
    status text NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','approved','rejected')),
    supplier_id uuid REFERENCES suppliers,
    created_at timestamptz NOT NULL DEFAULT now(),
    reviewed_at timestamptz,
    reviewed_by uuid REFERENCES users,
    review_reason text NOT NULL DEFAULT '',
    CHECK((status='pending' AND supplier_id IS NULL AND reviewed_at IS NULL AND reviewed_by IS NULL) OR
          (status='approved' AND supplier_id IS NOT NULL AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL) OR
          (status='rejected' AND supplier_id IS NULL AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL))
);
CREATE UNIQUE INDEX supplier_application_active_subject ON supplier_applications(auth_subject) WHERE status IN ('pending','approved');
CREATE UNIQUE INDEX supplier_application_active_email ON supplier_applications(lower(email)) WHERE status IN ('pending','approved');
CREATE INDEX supplier_application_review_queue ON supplier_applications(created_at) WHERE status='pending';
DO $$ DECLARE table_name text; BEGIN
    FOREACH table_name IN ARRAY ARRAY['schema_versions','suppliers','users','sessions','locations','vehicles','documents','quotes','bookings','allocations','booking_history','notification_jobs','audit_events','payments','payment_events','refunds','operations','commissions','supplier_applications'] LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',table_name);
    END LOOP;
END $$;
INSERT INTO schema_versions(version) VALUES (4);
COMMIT;
