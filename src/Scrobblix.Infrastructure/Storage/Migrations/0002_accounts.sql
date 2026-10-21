CREATE TABLE IF NOT EXISTS accounts (
    account_id TEXT PRIMARY KEY,
    provider TEXT NOT NULL,
    remote_username TEXT NOT NULL,
    credential_resource TEXT NOT NULL,
    is_connected INTEGER NOT NULL,
    connected_utc TEXT NULL,
    last_verified_utc TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    UNIQUE (provider, remote_username)
);

CREATE INDEX IF NOT EXISTS ix_accounts_provider_connected
    ON accounts (provider, is_connected);
