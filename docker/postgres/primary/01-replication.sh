#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'replicator') THEN
            CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replpass';
        END IF;
    END
    \$\$;
EOSQL

# Allow replication connections from the Docker network
echo "host replication replicator 0.0.0.0/0 md5" >> "${PGDATA}/pg_hba.conf"
echo "host all all 0.0.0.0/0 md5" >> "${PGDATA}/pg_hba.conf"
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -c "SELECT pg_reload_conf();"
