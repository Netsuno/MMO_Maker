-- Frog persistence v1 — PostgreSQL (idempotent).
-- Exécuté au démarrage du serveur si Postgres.enabled = true.
-- Ne contient aucun secret.

CREATE TABLE IF NOT EXISTS accounts(
    username TEXT PRIMARY KEY,
    password_hash TEXT NOT NULL,
    password_salt TEXT NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS player_world_state(
    username TEXT PRIMARY KEY REFERENCES accounts(username) ON DELETE CASCADE,
    map_id INT NOT NULL,
    pos_x INT NOT NULL,
    pos_y INT NOT NULL,
    updated_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS frog_map(
    id INT PRIMARY KEY,
    map_key TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    revision BIGINT NOT NULL DEFAULT 1,
    content_sha256 CHAR(64) NOT NULL,
    fmap_blob BYTEA NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_frog_map_updated_at ON frog_map(updated_at DESC);

CREATE TABLE IF NOT EXISTS frog_character(
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_username TEXT NOT NULL REFERENCES accounts(username) ON DELETE CASCADE,
    display_name TEXT NOT NULL,
    payload JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_frog_character_account_name UNIQUE (account_username, display_name)
);

CREATE INDEX IF NOT EXISTS idx_frog_character_account ON frog_character(account_username);

ALTER TABLE player_world_state
    ADD COLUMN IF NOT EXISTS character_uuid UUID REFERENCES frog_character(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_player_world_state_character ON player_world_state(character_uuid);

-- Binaires dédupliqués (tilesets, sprites) — référencés plus tard par manifest JSON côté frog_map ou table de liaison.
CREATE TABLE IF NOT EXISTS frog_asset_blob(
    content_sha256 CHAR(64) PRIMARY KEY,
    mime_type TEXT NOT NULL,
    bytes BYTEA NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS frog_map_editor_save(
    id BIGSERIAL PRIMARY KEY,
    map_id INT NOT NULL REFERENCES frog_map(id) ON DELETE CASCADE,
    account_username TEXT REFERENCES accounts(username) ON DELETE SET NULL,
    saved_revision BIGINT NOT NULL,
    client_comment TEXT,
    saved_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_frog_map_editor_save_map ON frog_map_editor_save(map_id, saved_at DESC);
