using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using InfraAis.Models;
using InfraAis.Options;

namespace InfraAis.Repositories;

public class AisRepository : IAisRepository
{
    private readonly string _connectionString;

    public AisRepository(IOptions<AisDbOptions> options)
    {
        _connectionString = options.Value.AisDb;
    }

    public async Task UpsertVesselAsync(VesselRecord vessel)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
MERGE vessels AS target
USING (SELECT @Mmsi AS mmsi) AS source
ON (target.mmsi = source.mmsi)
WHEN MATCHED THEN
    UPDATE SET
        imo           = COALESCE(@Imo, target.imo),
        name          = COALESCE(@Name, target.name),
        call_sign     = COALESCE(@CallSign, target.call_sign),
        ship_type     = COALESCE(@ShipType, target.ship_type),
        last_seen_utc = @TimestampUtc,
        updated_utc   = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (mmsi, imo, name, call_sign, ship_type, first_seen_utc, last_seen_utc, updated_utc)
    VALUES (@Mmsi, @Imo, @Name, @CallSign, @ShipType, @TimestampUtc, @TimestampUtc, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Mmsi", SqlDbType.BigInt).Value = vessel.Mmsi;
        cmd.Parameters.Add("@Imo", SqlDbType.Int).Value = (object?)vessel.Imo ?? DBNull.Value;
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = (object?)vessel.Name ?? DBNull.Value;
        cmd.Parameters.Add("@CallSign", SqlDbType.NVarChar, 16).Value = (object?)vessel.CallSign ?? DBNull.Value;
        cmd.Parameters.Add("@ShipType", SqlDbType.SmallInt).Value = (object?)vessel.ShipType ?? DBNull.Value;
        cmd.Parameters.Add("@TimestampUtc", SqlDbType.DateTime2).Value = vessel.TimestampUtc;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertPositionAsync(PositionRecord p)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO positions
    (mmsi, latitude, longitude, sog, cog, true_heading, nav_status, rate_of_turn, position_accuracy, msg_timestamp_utc)
VALUES
    (@Mmsi, @Latitude, @Longitude, @Sog, @Cog, @TrueHeading, @NavStatus, @RateOfTurn, @PositionAccuracy, @MsgTimestampUtc);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Mmsi", SqlDbType.BigInt).Value = p.Mmsi;
        cmd.Parameters.Add("@Latitude", SqlDbType.Float).Value = p.Latitude;
        cmd.Parameters.Add("@Longitude", SqlDbType.Float).Value = p.Longitude;
        cmd.Parameters.Add("@Sog", SqlDbType.Decimal).Value = (object?)p.Sog ?? DBNull.Value;
        cmd.Parameters.Add("@Cog", SqlDbType.Decimal).Value = (object?)p.Cog ?? DBNull.Value;
        cmd.Parameters.Add("@TrueHeading", SqlDbType.SmallInt).Value = (object?)p.TrueHeading ?? DBNull.Value;
        cmd.Parameters.Add("@NavStatus", SqlDbType.TinyInt).Value = (object?)p.NavStatus ?? DBNull.Value;
        cmd.Parameters.Add("@RateOfTurn", SqlDbType.SmallInt).Value = (object?)p.RateOfTurn ?? DBNull.Value;
        cmd.Parameters.Add("@PositionAccuracy", SqlDbType.Bit).Value = (object?)p.PositionAccuracy ?? DBNull.Value;
        cmd.Parameters.Add("@MsgTimestampUtc", SqlDbType.DateTime2).Value = p.MsgTimestampUtc;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertDeadLetterAsync(string rawPayload, string reason)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO ingestion_dead_letter (raw_payload, reason)
VALUES (@RawPayload, @Reason);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@RawPayload", SqlDbType.NVarChar, -1).Value = rawPayload;
        cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 256).Value = reason;

        await cmd.ExecuteNonQueryAsync();

    }
}

