-- Frog persistence v1 — MariaDB / InnoDB (idempotent).
-- Exécuté au démarrage du serveur si MariaDb.enabled = true.
-- Ne contient aucun secret. MariaDB 10.5+ recommandé (ADD COLUMN IF NOT EXISTS, CREATE INDEX IF NOT EXISTS).
--
-- Application manuelle (exemple) :
--   1) Créer la base si besoin : CREATE DATABASE votre_base CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
--   2) Choisir la base : USE votre_base;
--   3) Exécuter tout ce fichier (client mysql/mariadb, DBeaver, HeidiSQL, etc.).
-- La contrainte fk_pws_character (player_world_state → frog_character) est ajoutée par le serveur
-- au premier démarrage (MariaDbSchemaBootstrap) si elle n’existe pas encore.

CREATE TABLE IF NOT EXISTS accounts(
    username VARCHAR(255) NOT NULL PRIMARY KEY,
    password_hash VARCHAR(512) NOT NULL,
    password_salt VARCHAR(512) NOT NULL,
    created_utc DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS player_world_state(
    username VARCHAR(255) NOT NULL PRIMARY KEY,
    map_id INT NOT NULL,
    pos_x INT NOT NULL,
    pos_y INT NOT NULL,
    updated_utc DATETIME(6) NOT NULL,
    CONSTRAINT fk_pws_account FOREIGN KEY (username) REFERENCES accounts(username) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS frog_map(
    id INT NOT NULL PRIMARY KEY,
    map_key VARCHAR(255) NOT NULL,
    display_name VARCHAR(512) NOT NULL,
    revision BIGINT NOT NULL DEFAULT 1,
    content_sha256 CHAR(64) NOT NULL,
    fmap_blob LONGBLOB NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_frog_map_map_key(map_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IF NOT EXISTS idx_frog_map_updated_at ON frog_map(updated_at DESC);

CREATE TABLE IF NOT EXISTS frog_character(
    id CHAR(36) NOT NULL PRIMARY KEY,
    account_username VARCHAR(255) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    payload JSON NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_frog_character_account_name(account_username, display_name),
    CONSTRAINT fk_fc_account FOREIGN KEY (account_username) REFERENCES accounts(username) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IF NOT EXISTS idx_frog_character_account ON frog_character(account_username);

-- Position monde par personnage (multi-slots : une ligne par frog_character.id).
CREATE TABLE IF NOT EXISTS character_world_state(
    character_uuid CHAR(36) NOT NULL PRIMARY KEY,
    map_id INT NOT NULL,
    pos_x INT NOT NULL,
    pos_y INT NOT NULL,
    updated_utc DATETIME(6) NOT NULL,
    CONSTRAINT fk_cws_character FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE player_world_state
    ADD COLUMN IF NOT EXISTS character_uuid CHAR(36) NULL;

CREATE TABLE IF NOT EXISTS frog_asset_blob(
    content_sha256 CHAR(64) NOT NULL PRIMARY KEY,
    mime_type VARCHAR(128) NOT NULL,
    bytes LONGBLOB NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS frog_map_editor_save(
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    map_id INT NOT NULL,
    account_username VARCHAR(255) NULL,
    saved_revision BIGINT NOT NULL,
    client_comment VARCHAR(512) NULL,
    saved_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_fmes_map FOREIGN KEY (map_id) REFERENCES frog_map(id) ON DELETE CASCADE,
    CONSTRAINT fk_fmes_account FOREIGN KEY (account_username) REFERENCES accounts(username) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IF NOT EXISTS idx_frog_map_editor_save_map ON frog_map_editor_save(map_id, saved_at DESC);

-- Catalogue d’événements réutilisables + placement sur carte (Phase 3 MVP).
CREATE TABLE IF NOT EXISTS frog_event_catalog(
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    slug VARCHAR(64) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_frog_event_catalog_slug(slug)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS frog_map_event(
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    map_id INT NOT NULL,
    event_catalog_id INT NOT NULL,
    tile_x INT NOT NULL,
    tile_y INT NOT NULL,
    trigger_kind VARCHAR(32) NOT NULL DEFAULT 'interact' COMMENT 'interact | step_on | page',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_fme_map FOREIGN KEY (map_id) REFERENCES frog_map(id) ON DELETE CASCADE,
    CONSTRAINT fk_fme_cat FOREIGN KEY (event_catalog_id) REFERENCES frog_event_catalog(id) ON DELETE CASCADE,
    UNIQUE KEY uq_frog_map_event_cell(map_id, tile_x, tile_y, event_catalog_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IF NOT EXISTS idx_frog_map_event_map ON frog_map_event(map_id);
