#!/usr/bin/env bash
set -eo pipefail

# Runs only when PostgreSQL initializes a new, empty volume.
# The API uses a database owner role without cluster-wide administrative rights.
# The documented format is a single line; accept Windows CRLF endings too.
app_password="$(tr -d '\r\n' < /run/secrets/app_db_password)"
if [ -z "$app_password" ]; then
  echo "Application database password must not be empty." >&2
  exit 1
fi
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=ON_ERROR_STOP=1 --set=app_password="$app_password" <<'SQL'
SELECT format('CREATE ROLE inspectnow_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L', :'app_password') \gexec
ALTER DATABASE inspectnow OWNER TO inspectnow_app;
SQL
unset app_password
