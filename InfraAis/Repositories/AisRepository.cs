using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using InfraAis.Models;
using InfraAis.Options;
using InfraAis.Utils;
using Dapper;
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
MERGE vessels WITH (HOLDLOCK) AS target
USING (SELECT @Mmsi AS mmsi) AS source
ON (target.mmsi = source.mmsi)
WHEN MATCHED THEN
    UPDATE SET
        imo              = COALESCE(@Imo, target.imo),
        name             = COALESCE(@Name, target.name),
        call_sign        = COALESCE(@CallSign, target.call_sign),
        ship_type        = COALESCE(@ShipType, target.ship_type),
        dim_to_bow       = COALESCE(@DimToBow, target.dim_to_bow),
        dim_to_stern     = COALESCE(@DimToStern, target.dim_to_stern),
        dim_to_port      = COALESCE(@DimToPort, target.dim_to_port),
        dim_to_starboard = COALESCE(@DimToStarboard, target.dim_to_starboard),
        draught          = COALESCE(@Draught, target.draught),
        destination      = COALESCE(@Destination, target.destination),
        eta              = COALESCE(@Eta, target.eta),
        last_seen_utc    = @TimestampUtc,
        updated_utc      = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (mmsi, imo, name, call_sign, ship_type,
            dim_to_bow, dim_to_stern, dim_to_port, dim_to_starboard,
            draught, destination, eta,
            first_seen_utc, last_seen_utc, updated_utc)
    VALUES (@Mmsi, @Imo, @Name, @CallSign, @ShipType,
            @DimToBow, @DimToStern, @DimToPort, @DimToStarboard,
            @Draught, @Destination, @Eta,
            @TimestampUtc, @TimestampUtc, SYSUTCDATETIME());";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Mmsi", SqlDbType.BigInt).Value = vessel.Mmsi;
        cmd.Parameters.Add("@Imo", SqlDbType.Int).Value = (object?)vessel.Imo ?? DBNull.Value;
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = (object?)vessel.Name ?? DBNull.Value;
        cmd.Parameters.Add("@CallSign", SqlDbType.NVarChar, 16).Value = (object?)vessel.CallSign ?? DBNull.Value;
        cmd.Parameters.Add("@ShipType", SqlDbType.SmallInt).Value = (object?)(short?)vessel.ShipType ?? DBNull.Value;
        cmd.Parameters.Add("@DimToBow", SqlDbType.SmallInt).Value = (object?)vessel.DimToBow ?? DBNull.Value;
        cmd.Parameters.Add("@DimToStern", SqlDbType.SmallInt).Value = (object?)vessel.DimToStern ?? DBNull.Value;
        cmd.Parameters.Add("@DimToPort", SqlDbType.SmallInt).Value = (object?)vessel.DimToPort ?? DBNull.Value;
        cmd.Parameters.Add("@DimToStarboard", SqlDbType.SmallInt).Value = (object?)vessel.DimToStarboard ?? DBNull.Value;

        var draught = cmd.Parameters.Add("@Draught", SqlDbType.Decimal);
        draught.Precision = 4;
        draught.Scale = 1;
        draught.Value = (object?)vessel.Draught ?? DBNull.Value;

        cmd.Parameters.Add("@Destination", SqlDbType.NVarChar, 64).Value = (object?)vessel.Destination ?? DBNull.Value;
        cmd.Parameters.Add("@Eta", SqlDbType.DateTime2).Value = (object?)vessel.Eta ?? DBNull.Value;
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
    public async Task<long?> GetMmsiByImoAsync(int imo)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"SELECT mmsi FROM vessels WHERE imo = @imo;", conn);
        cmd.Parameters.AddWithValue("@imo", imo);

        var result = await cmd.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt64(result);

    }

    public async Task<LatestPositionRecord?> GetLatestPositionAsync(long mmsi)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"SELECT TOP (1)
    p.mmsi, p.latitude, p.longitude, p.sog, p.cog, p.nav_status, p.msg_timestamp_utc,v.imo,v.name
FROM positions p
JOIN vessels V ON v.mmsi = p.mmsi
WHERE p.mmsi = @mmsi
ORDER BY p.msg_timestamp_utc DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@mmsi", mmsi);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())

            return null;
        return new LatestPositionRecord
        {
            Mmsi = reader.GetInt64(reader.GetOrdinal("mmsi")),
            Latitude = reader.GetDouble(reader.GetOrdinal("latitude")),
            Longitude = reader.GetDouble(reader.GetOrdinal("longitude")),
            Sog = reader.IsDBNull(reader.GetOrdinal("sog")) ? null : reader.GetDecimal(reader.GetOrdinal("sog")),
            Cog = reader.IsDBNull(reader.GetOrdinal("cog")) ? null : reader.GetDecimal(reader.GetOrdinal("cog")),
            NavStatus = reader.IsDBNull(reader.GetOrdinal("nav_status")) ? null : reader.GetByte(reader.GetOrdinal("nav_status")),
            TimestampUtc = reader.GetDateTime(reader.GetOrdinal("msg_timestamp_utc")),
            Imo = reader.IsDBNull(reader.GetOrdinal("imo")) ? null : reader.GetInt32(reader.GetOrdinal("imo")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"))
        };





    }
    public async Task<VesselRecord?> GetVesselAsync(long mmsi)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        const string sql = @"SELECT mmsi, imo, name, call_sign, ship_type,dim_to_bow, dim_to_stern, dim_to_port, dim_to_starboard,draught, destination, eta, first_seen_utc, last_seen_utc
FROM vessels
WHERE mmsi = @Mmsi;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("mmsi", mmsi);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new VesselRecord
        {
            Mmsi = reader.GetInt64(reader.GetOrdinal("mmsi")),
            Imo = reader.IsDBNull(reader.GetOrdinal("imo")) ? null : reader.GetInt32(reader.GetOrdinal("imo")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            CallSign = reader.IsDBNull(reader.GetOrdinal("call_sign")) ? null : reader.GetString(reader.GetOrdinal("call_sign")),
            ShipType = reader.IsDBNull(reader.GetOrdinal("ship_type")) ? null : reader.GetInt16(reader.GetOrdinal("ship_type")),
            DimToBow = reader.IsDBNull(reader.GetOrdinal("dim_to_bow")) ? null : reader.GetInt16(reader.GetOrdinal("dim_to_bow")),
            DimToStern = reader.IsDBNull(reader.GetOrdinal("dim_to_stern")) ? null : reader.GetInt16(reader.GetOrdinal("dim_to_stern")),
            DimToPort = reader.IsDBNull(reader.GetOrdinal("dim_to_port")) ? null : reader.GetInt16(reader.GetOrdinal("dim_to_port")),
            DimToStarboard = reader.IsDBNull(reader.GetOrdinal("dim_to_starboard")) ? null : reader.GetInt16(reader.GetOrdinal("dim_to_starboard")),
            Draught = reader.IsDBNull(reader.GetOrdinal("draught")) ? null : reader.GetDecimal(reader.GetOrdinal("draught")),
            Destination = reader.IsDBNull(reader.GetOrdinal("destination")) ? null : reader.GetString(reader.GetOrdinal("destination")),
            Eta = reader.IsDBNull(reader.GetOrdinal("eta")) ? null : reader.GetDateTime(reader.GetOrdinal("eta")),
            TimestampUtc = reader.GetDateTime(reader.GetOrdinal("last_seen_utc")),
            FirstSeenUtc = reader.GetDateTime(reader.GetOrdinal("first_seen_utc")),
            LastSeenUtc = reader.GetDateTime(reader.GetOrdinal("last_seen_utc")),
        };
    }


    public async Task<PagedResponse> GetPositionsAsync(
        PositionQueryFilters filters)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $@"
        SELECT TOP (@Limit+1)
            mmsi AS Mmsi,
            latitude AS Latitude,
            longitude AS Longitude,
            sog AS Sog,
            cog AS Cog,
            true_heading AS TrueHeading,
            nav_status AS NavStatus,
            rate_of_turn AS RateOfTurn,
            position_accuracy AS PositionAccuracy,
            msg_timestamp_utc AS MsgTimestampUtc,
            id AS Id
        FROM positions
        WHERE 1 = 1
    ";

        var parameters = new DynamicParameters();

        parameters.Add("Limit", filters.Limit);


        if (filters.From.HasValue && filters.To.HasValue)
        {
            sql += @"
            AND msg_timestamp_utc >= @From
            AND msg_timestamp_utc < @To
        ";

            parameters.Add("From", filters.From);
            parameters.Add("To", filters.To);
        }


        if (filters.BoundingBox is not null)
        {
            sql += @"
            AND latitude >= @MinLat
            AND latitude <= @MaxLat
            AND longitude >= @MinLon
            AND longitude <= @MaxLon
        ";

            parameters.Add("MinLat", filters.BoundingBox.MinLat);
            parameters.Add("MaxLat", filters.BoundingBox.MaxLat);
            parameters.Add("MinLon", filters.BoundingBox.MinLon);
            parameters.Add("MaxLon", filters.BoundingBox.MaxLon);
        }

        if (filters.Cursor is not null)
        {
            var cursor = CursorCodec.Decode(filters.Cursor);

            if (cursor is not null)
            {
                if (filters.Sort == SortDirection.Desc)
                {
                    sql += @"
                AND (
                    msg_timestamp_utc < @CursorTs
                    OR (
                        msg_timestamp_utc = @CursorTs
                        AND id < @CursorId
                    )
                )
            ";
                }
                else
                {
                    sql += @"
                AND (
                    msg_timestamp_utc > @CursorTs
                    OR (
                        msg_timestamp_utc = @CursorTs
                        AND id > @CursorId
                    )
                )
            ";
                }

                parameters.Add("CursorTs", cursor.Timestamp);
                parameters.Add("CursorId", cursor.Id);
            }
        }
        if (filters.Mmsis is not null && filters.Mmsis.Count > 0)
        {
            sql += " AND mmsi IN @Mmsis";
            parameters.Add("Mmsis", filters.Mmsis);
        }


        if (filters.NavStatuses is not null && filters.NavStatuses.Count > 0)
        {
            sql += " AND nav_status IN @NavStatuses";
            parameters.Add("NavStatuses", filters.NavStatuses);
        }


        if (filters.MinSog.HasValue)
        {
            sql += " AND sog >= @MinSog";
            parameters.Add("MinSog", filters.MinSog);
        }


        if (filters.MaxSog.HasValue)
        {
            sql += " AND sog <= @MaxSog";
            parameters.Add("MaxSog", filters.MaxSog);
        }

        var direction = filters.Sort.ToString().ToUpperInvariant();

        sql += $@"
    ORDER BY
        msg_timestamp_utc {direction},
        id {direction};
";

        var positions =
        (await conn.QueryAsync<PositionHistoryRecord>(sql, parameters))
        .ToList();

        var hasMore = positions.Count > filters.Limit;

        if (hasMore)
        {
            positions.RemoveAt(positions.Count - 1);
        }

        string? nextCursor = null;

        if (hasMore && positions.Count > 0)
        {
            var lastPosition = positions[^1];

            nextCursor = CursorCodec.Encode(
                new PositionCursor
                {
                    Timestamp = lastPosition.MsgTimestampUtc,
                    Id = lastPosition.Id
                });
        }

        return new PagedResponse
        {
            Items = positions,
            NextCursor = nextCursor,
            Count = positions.Count
        };
    }

  public async Task<PagedResponse> GetPositionsByMmsiAsync(
    long mmsi,
    PositionQueryFilters filters)
{
    await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    var sql = @"
        SELECT TOP (@Limit + 1)
            mmsi AS Mmsi,
            latitude AS Latitude,
            longitude AS Longitude,
            sog AS Sog,
            cog AS Cog,
            true_heading AS TrueHeading,
            nav_status AS NavStatus,
            rate_of_turn AS RateOfTurn,
            position_accuracy AS PositionAccuracy,
            msg_timestamp_utc AS MsgTimestampUtc,
            id AS Id
        FROM positions
        WHERE mmsi = @Mmsi
    ";

    var parameters = new DynamicParameters();

    parameters.Add("Mmsi", mmsi);
    parameters.Add("Limit", filters.Limit);

    // FROM μόνο του επιτρέπεται
    if (filters.From.HasValue)
    {
        sql += @"
            AND msg_timestamp_utc >= @From
        ";

        parameters.Add("From", filters.From.Value.UtcDateTime);
    }

    // TO μόνο του επιτρέπεται
    if (filters.To.HasValue)
    {
        sql += @"
            AND msg_timestamp_utc < @To
        ";

        parameters.Add("To", filters.To.Value.UtcDateTime);
    }

    // Bounding box
    if (filters.BoundingBox is not null)
    {
        sql += @"
            AND latitude >= @MinLat
            AND latitude <= @MaxLat
            AND longitude >= @MinLon
            AND longitude <= @MaxLon
        ";

        parameters.Add("MinLat", filters.BoundingBox.MinLat);
        parameters.Add("MaxLat", filters.BoundingBox.MaxLat);
        parameters.Add("MinLon", filters.BoundingBox.MinLon);
        parameters.Add("MaxLon", filters.BoundingBox.MaxLon);
    }

    // Nav status
    if (filters.NavStatuses is not null &&
        filters.NavStatuses.Count > 0)
    {
        sql += @"
            AND nav_status IN @NavStatuses
        ";

        parameters.Add("NavStatuses", filters.NavStatuses);
    }

    // Minimum SOG
    if (filters.MinSog.HasValue)
    {
        sql += @"
            AND sog >= @MinSog
        ";

        parameters.Add("MinSog", filters.MinSog);
    }

    // Maximum SOG
    if (filters.MaxSog.HasValue)
    {
        sql += @"
            AND sog <= @MaxSog
        ";

        parameters.Add("MaxSog", filters.MaxSog);
    }

    // Cursor pagination
    if (filters.Cursor is not null)
    {
        var cursor = CursorCodec.Decode(filters.Cursor);

        if (cursor is not null)
        {
            if (filters.Sort == SortDirection.Desc)
            {
                sql += @"
                    AND (
                        msg_timestamp_utc < @CursorTs
                        OR (
                            msg_timestamp_utc = @CursorTs
                            AND id < @CursorId
                        )
                    )
                ";
            }
            else
            {
                sql += @"
                    AND (
                        msg_timestamp_utc > @CursorTs
                        OR (
                            msg_timestamp_utc = @CursorTs
                            AND id > @CursorId
                        )
                    )
                ";
            }

            parameters.Add("CursorTs", cursor.Timestamp);
            parameters.Add("CursorId", cursor.Id);
        }
    }

    var direction =
        filters.Sort.ToString().ToUpperInvariant();

    sql += $@"
        ORDER BY
            msg_timestamp_utc {direction},
            id {direction};
    ";

    var positions =
        (await conn.QueryAsync<PositionHistoryRecord>(
            sql,
            parameters))
        .ToList();

    var hasMore =
        positions.Count > filters.Limit;

    if (hasMore)
    {
        positions.RemoveAt(
            positions.Count - 1);
    }

    string? nextCursor = null;

    if (hasMore && positions.Count > 0)
    {
        var lastPosition =
            positions[^1];

        nextCursor =
            CursorCodec.Encode(
                new PositionCursor
                {
                    Timestamp =
                        lastPosition.MsgTimestampUtc,
                    Id =
                        lastPosition.Id
                });
    }

    return new PagedResponse
    {
        Items = positions,
        NextCursor = nextCursor,
        Count = positions.Count
    };
}
}
