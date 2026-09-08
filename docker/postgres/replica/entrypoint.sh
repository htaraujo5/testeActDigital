#!/bin/bash
set -euo pipefail

PRIMARY_HOST="${PRIMARY_HOST:-postgres-write}"
PRIMARY_PORT="${PRIMARY_PORT:-5432}"
REPLICATOR_USER="${REPLICATOR_USER:-replicator}"
REPLICATOR_PASSWORD="${REPLICATOR_PASSWORD:-replpass}"
PGDATA="${PGDATA:-/var/lib/postgresql/data}"

echo "Waiting for primary ${PRIMARY_HOST}:${PRIMARY_PORT}..."
until pg_isready -h "$PRIMARY_HOST" -p "$PRIMARY_PORT" -U account >/dev/null 2>&1; do
  sleep 2
done

# Give primary a moment after readiness (init scripts / reload)
sleep 3

if [ ! -s "${PGDATA}/PG_VERSION" ]; then
  echo "Taking base backup from primary..."
  rm -rf "${PGDATA:?}/"*
  export PGPASSWORD="$REPLICATOR_PASSWORD"
  pg_basebackup \
    -h "$PRIMARY_HOST" \
    -p "$PRIMARY_PORT" \
    -U "$REPLICATOR_USER" \
    -D "$PGDATA" \
    -Fp -Xs -P -R
  unset PGPASSWORD
  echo "Base backup completed."
fi

echo "Starting PostgreSQL replica..."
exec docker-entrypoint.sh postgres -c hot_standby=on -c primary_conninfo="host=${PRIMARY_HOST} port=${PRIMARY_PORT} user=${REPLICATOR_USER} password=${REPLICATOR_PASSWORD}"
