#!/bin/sh
# Identity's database roles (ADR-0006). Runs only when the PostgreSQL volume is empty.
# identity_migrator owns the schema; identity_app is the runtime role and cannot bypass RLS.
set -eu

: "${IDENTITY_MIGRATOR_PASSWORD:?IDENTITY_MIGRATOR_PASSWORD is required}"
: "${IDENTITY_APP_PASSWORD:?IDENTITY_APP_PASSWORD is required}"

psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    -v database="$POSTGRES_DB" \
    -v migrator_password="$IDENTITY_MIGRATOR_PASSWORD" \
    -v app_password="$IDENTITY_APP_PASSWORD" <<'SQL'
CREATE ROLE identity_migrator LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD :'migrator_password';
CREATE ROLE identity_app LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD :'app_password';

REVOKE CONNECT ON DATABASE :"database" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"database" TO identity_migrator, identity_app;

ALTER SCHEMA public OWNER TO identity_migrator;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO identity_app;

ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO identity_app;
SQL
