-- Create Frog least-privilege LOGIN roles (cluster-level).
-- Passwords are NOT stored here — scripts/postgres-apply-roles.sh sets them.
-- Roles:
--   frog_runtime  — server runtime (DML, no DDL)  [required]
--   frog_migrate  — EF migrations / schema        [required]
--   frog_publish  — editor publish                [required]
--   frog_ops      — OpsCli (recommended)
-- All: LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE.
-- docker-compose POSTGRES_USER=frog remains instance superuser for local demo only.

DO $frog_roles$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'frog_runtime') THEN
    CREATE ROLE frog_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  ELSE
    ALTER ROLE frog_runtime WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'frog_migrate') THEN
    CREATE ROLE frog_migrate LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE INHERIT;
  ELSE
    ALTER ROLE frog_migrate WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE INHERIT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'frog_publish') THEN
    CREATE ROLE frog_publish LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  ELSE
    ALTER ROLE frog_publish WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'frog_ops') THEN
    CREATE ROLE frog_ops LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  ELSE
    ALTER ROLE frog_ops WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
  END IF;
END
$frog_roles$;
