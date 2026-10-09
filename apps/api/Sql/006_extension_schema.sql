BEGIN;
CREATE SCHEMA IF NOT EXISTS extensions;
ALTER EXTENSION btree_gist SET SCHEMA extensions;
INSERT INTO schema_versions(version) VALUES (6);
COMMIT;
