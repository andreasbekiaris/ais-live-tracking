USE InfralabsDB;
GO

DROP TABLE positions;
GO

CREATE TABLE positions (
    Pos_name NVARCHAR(64) PRIMARY KEY NOT NULL,
    pos_lat FLOAT NOT NULL,
    pos_lon FLOAT NOT NULL
);
GO
CREATE TABLE routes (
    route_id      INT IDENTITY(1,1) PRIMARY KEY,
    route_name    NVARCHAR(128) NOT NULL UNIQUE,
    origin_name   NVARCHAR(64)  NOT NULL REFERENCES positions(pos_name),
    dest_name     NVARCHAR(64)  NOT NULL REFERENCES positions(pos_name),
    created_at    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);
GO 
CREATE TABLE shipments (
    shipment_id    INT IDENTITY(1,1) PRIMARY KEY,
    tracking_code  NVARCHAR(32)  NOT NULL UNIQUE,
    route_id       INT           NOT NULL REFERENCES routes(route_id),
    status         NVARCHAR(16)  NOT NULL DEFAULT 'pending',
                  -- allowed values: pending | in_transit | delivered | cancelled
    created_at     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    updated_at     DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);
