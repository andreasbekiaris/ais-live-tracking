CREATE DATABASE AisTrackingDB;
GO
USE AisTrackingDB;
GO

CREATE TABLE vessels (
    mmsi             BIGINT        NOT NULL PRIMARY KEY,
    imo              INT           NULL,
    name             NVARCHAR(128) NULL,
    call_sign        NVARCHAR(16)  NULL,
    ship_type        SMALLINT      NULL,
    dim_to_bow       SMALLINT      NULL,
    dim_to_stern     SMALLINT      NULL,
    dim_to_port      SMALLINT      NULL,
    dim_to_starboard SMALLINT      NULL,
    draught          DECIMAL(4,1)  NULL,
    destination      NVARCHAR(64)  NULL,
    eta              DATETIME2     NULL,
    first_seen_utc   DATETIME2     NOT NULL,
    last_seen_utc    DATETIME2     NOT NULL,
    updated_utc      DATETIME2     NOT NULL
        CONSTRAINT DF_vessels_updated DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UX_vessels_imo ON vessels(imo) WHERE imo IS NOT NULL;

CREATE TABLE positions (
    id                BIGINT       IDENTITY(1,1) PRIMARY KEY,
    mmsi              BIGINT       NOT NULL,
    latitude          FLOAT        NOT NULL,
    longitude         FLOAT        NOT NULL,
    sog               DECIMAL(5,1) NULL,
    cog               DECIMAL(5,1) NULL,
    true_heading      SMALLINT     NULL,
    nav_status        TINYINT      NULL,
    rate_of_turn      SMALLINT     NULL,
    position_accuracy BIT          NULL,
    msg_timestamp_utc DATETIME2    NOT NULL,
    received_utc      DATETIME2    NOT NULL
        CONSTRAINT DF_positions_received DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_positions_vessels FOREIGN KEY (mmsi) REFERENCES vessels(mmsi)
);

CREATE INDEX IX_positions_mmsi_time
    ON positions(mmsi, msg_timestamp_utc DESC)
    INCLUDE (latitude, longitude, sog, cog, nav_status);

-- Phase 3: time-first composite for windowed fleet scans (GET /api/positions).
CREATE INDEX IX_positions_time
    ON positions(msg_timestamp_utc)
    INCLUDE (mmsi, latitude, longitude, sog, nav_status);

CREATE TABLE ingestion_dead_letter (
    id           BIGINT        IDENTITY(1,1) PRIMARY KEY,
    raw_payload  NVARCHAR(MAX) NOT NULL,
    reason       NVARCHAR(256) NOT NULL,
    received_utc DATETIME2     NOT NULL
        CONSTRAINT DF_dlq_received DEFAULT SYSUTCDATETIME()
);
