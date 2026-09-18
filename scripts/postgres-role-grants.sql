-- Least-privilege grants for Frog product schemas (auth, content, ops, player, world).
-- Invoked after LOGIN roles exist. Role names come from custom GUCs (session):
--   frog.runtime_role  frog.migrate_role  frog.publish_role  frog.ops_role
-- Apply via scripts/postgres-apply-roles.sh or IsolatedPostgres tests.
-- frog_runtime: DML only (SELECT/INSERT/UPDATE/DELETE), never DDL / CREATEROLE / super-user.
-- frog_migrate: DDL + DML on product schemas (EF migrations).
-- frog_publish: DML on content + world (éditeur).
-- frog_ops: recommended OpsCli role — DML on auth + ops.

DO $frog_grants$
DECLARE
  runtime_role text := current_setting('frog.runtime_role', true);
  migrate_role text := current_setting('frog.migrate_role', true);
  publish_role text := current_setting('frog.publish_role', true);
  ops_role text := current_setting('frog.ops_role', true);
  db_name text := current_database();
  sch text;
  product_schemas text[] := ARRAY['auth', 'content', 'ops', 'player', 'world'];
BEGIN
  IF runtime_role IS NULL OR runtime_role = '' THEN
    RAISE EXCEPTION 'frog.runtime_role GUC is required';
  END IF;
  IF migrate_role IS NULL OR migrate_role = '' THEN
    RAISE EXCEPTION 'frog.migrate_role GUC is required';
  END IF;
  IF publish_role IS NULL OR publish_role = '' THEN
    RAISE EXCEPTION 'frog.publish_role GUC is required';
  END IF;

  EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I, %I, %I', db_name, runtime_role, migrate_role, publish_role);
  IF ops_role IS NOT NULL AND ops_role <> '' THEN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', db_name, ops_role);
  END IF;

  -- public: EF __EFMigrationsHistory only. Runtime must not CREATE here.
  EXECUTE format('REVOKE CREATE ON SCHEMA public FROM PUBLIC, %I, %I', runtime_role, publish_role);
  EXECUTE format('GRANT USAGE ON SCHEMA public TO %I, %I, %I', runtime_role, migrate_role, publish_role);
  EXECUTE format('GRANT CREATE ON SCHEMA public TO %I', migrate_role);
  EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA public TO %I, %I', runtime_role, publish_role);
  EXECUTE format('GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO %I', migrate_role);
  EXECUTE format('GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO %I', migrate_role);
  EXECUTE format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO %I',
    migrate_role, migrate_role);
  EXECUTE format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT ON TABLES TO %I, %I',
    migrate_role, runtime_role, publish_role);

  IF ops_role IS NOT NULL AND ops_role <> '' THEN
    EXECUTE format('REVOKE CREATE ON SCHEMA public FROM %I', ops_role);
    EXECUTE format('GRANT USAGE ON SCHEMA public TO %I', ops_role);
    EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA public TO %I', ops_role);
    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT ON TABLES TO %I',
      migrate_role, ops_role);
  END IF;

  FOREACH sch IN ARRAY product_schemas
  LOOP
    EXECUTE format('REVOKE ALL ON SCHEMA %I FROM PUBLIC', sch);
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I, %I, %I', sch, runtime_role, migrate_role, publish_role);
    EXECUTE format('GRANT CREATE ON SCHEMA %I TO %I', sch, migrate_role);
    EXECUTE format('REVOKE CREATE ON SCHEMA %I FROM %I, %I', sch, runtime_role, publish_role);

    -- Runtime: DML, never TRUNCATE / REFERENCES / TRIGGER / DDL.
    EXECUTE format(
      'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I',
      sch, runtime_role);
    EXECUTE format(
      'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I',
      sch, runtime_role);

    EXECUTE format('GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA %I TO %I', sch, migrate_role);
    EXECUTE format('GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA %I TO %I', sch, migrate_role);

    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
      migrate_role, sch, runtime_role);
    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I',
      migrate_role, sch, runtime_role);
    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT ALL PRIVILEGES ON TABLES TO %I',
      migrate_role, sch, migrate_role);
    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT ALL PRIVILEGES ON SEQUENCES TO %I',
      migrate_role, sch, migrate_role);
  END LOOP;

  -- Publisher: write content + world (snapshots publiés). Read the rest.
  FOREACH sch IN ARRAY ARRAY['content', 'world']
  LOOP
    EXECUTE format(
      'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I',
      sch, publish_role);
    EXECUTE format(
      'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I',
      sch, publish_role);
    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
      migrate_role, sch, publish_role);
  END LOOP;

  FOREACH sch IN ARRAY ARRAY['auth', 'ops', 'player']
  LOOP
    EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA %I TO %I', sch, publish_role);
    EXECUTE format(
      'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT ON TABLES TO %I',
      migrate_role, sch, publish_role);
  END LOOP;

  IF ops_role IS NOT NULL AND ops_role <> '' THEN
    FOREACH sch IN ARRAY product_schemas
    LOOP
      EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', sch, ops_role);
      EXECUTE format('REVOKE CREATE ON SCHEMA %I FROM %I', sch, ops_role);
    END LOOP;

    FOREACH sch IN ARRAY ARRAY['auth', 'ops']
    LOOP
      EXECUTE format(
        'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I',
        sch, ops_role);
      EXECUTE format(
        'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I',
        sch, ops_role);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
        migrate_role, sch, ops_role);
    END LOOP;

    FOREACH sch IN ARRAY ARRAY['content', 'player', 'world']
    LOOP
      EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA %I TO %I', sch, ops_role);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT ON TABLES TO %I',
        migrate_role, sch, ops_role);
    END LOOP;
  END IF;
END
$frog_grants$;
