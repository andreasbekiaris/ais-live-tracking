using InfraAis.Options;
using InfraAis.Validation;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using InfraAis.Models;
using Microsoft.Extensions.Options;
using InfraAis.Repositories;
namespace InfraAis.Services;

public class AisIngestionService : BackgroundService
{
    private readonly ILogger<AisIngestionService> _logger;
    private readonly AisStreamOptions _options;

    private readonly IngestionOptions _ingestion;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private long _received;
    private long _stored;
    private long _rejected;
    private long _staticStored;
    private long _reconnects;
    public AisIngestionService(
     ILogger<AisIngestionService> logger,
     IOptions<AisStreamOptions> options,
     IOptions<IngestionOptions> ingestion,
      IServiceScopeFactory scopeFactory,
      IHostEnvironment environment)
    {
        _logger = logger;
        _options = options.Value;
        _ingestion = ingestion.Value;
        _scopeFactory = scopeFactory;
        _environment = environment;
    }
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = RunSummaryLoopAsync(stoppingToken);
        var delay = _ingestion.ReconnectBaseDelaySeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(stoppingToken);

                delay = _ingestion.ReconnectBaseDelaySeconds;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _reconnects++;
                _logger.LogError(ex, "Feed connection failed. Reconnecting in {Delay}s...", delay);

                await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);

                delay = Math.Min(delay * 2, _ingestion.ReconnectMaxDelaySeconds);
            }
        }

        _logger.LogInformation("AIS worker stopping.");
    }

    private async Task ConnectAndListenAsync(CancellationToken stoppingToken)
    {
        using var ws = new ClientWebSocket();

        if (_options.DangerouslyAcceptInvalidFeedCertificate && _environment.IsDevelopment())
        {
            _logger.LogWarning(
                "TLS certificate validation is DISABLED for the AIS feed (development only). " +
                "Unset AisStream:DangerouslyAcceptInvalidFeedCertificate once the upstream certificate is renewed.");
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }
        else if (_options.DangerouslyAcceptInvalidFeedCertificate)
        {
            _logger.LogError(
                "AisStream:DangerouslyAcceptInvalidFeedCertificate is set but the environment is {Environment}, not Development. " +
                "Certificate validation stays ENABLED.", _environment.EnvironmentName);
        }

        _logger.LogInformation("Connecting to AIS feed: {Url}", _options.Url);
        await ws.ConnectAsync(new Uri(_options.Url), stoppingToken);
        _logger.LogInformation("Connected. Sending subscription...");

        var box = _options.BoundingBox;
        var subscription = new
        {
            APIKey = _options.ApiKey,
            BoundingBoxes = new[] { new[] { new[] { box.MinLat, box.MinLon }, new[] { box.MaxLat, box.MaxLon } } },
            FilterMessageTypes = _options.FilterMessageTypes
        };
        var json = JsonSerializer.Serialize(subscription);
        var sendBytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(sendBytes, WebSocketMessageType.Text, endOfMessage: true, stoppingToken);
        _logger.LogInformation("Subscription sent. Listening...");

        var buffer = new byte[8192];
        while (!stoppingToken.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, stoppingToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("Server closed the connection.");
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var message = Encoding.UTF8.GetString(ms.ToArray());
            _received++;
            try
            {
                var ais = JsonSerializer.Deserialize<AisMessage>(message, _jsonOptions);

                switch (ais?.MessageType)
                {
                    case "PositionReport":
                        await HandlePositionReport(ais, message);
                        break;
                    case "ShipStaticData":
                        await HandleStaticData(ais, message);
                        break;
                    default:
                        _logger.LogDebug("Skipping type: {Type}", ais?.MessageType);
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Bad message skipped: {Error}", ex.Message);
            }
        }
    }
    private async Task HandlePositionReport(AisMessage msg, string rawPayload)
    {
        var pr = msg.Message.PositionReport;
        if (pr is null) return;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAisRepository>();

        if (!pr.Valid)
        {
            await repo.InsertDeadLetterAsync(rawPayload, "feed flagged invalid");
            _rejected++;
            _logger.LogWarning("REJECT: feed flagged invalid. MMSI={Mmsi}", pr.UserID);
            return;
        }

        if (!AisValidator.IsValidMmsi(pr.UserID))
        {
            await repo.InsertDeadLetterAsync(rawPayload, $"bad MMSI {pr.UserID}");
            _rejected++;
            _logger.LogWarning("REJECT: bad MMSI={Mmsi}", pr.UserID);
            return;
        }

        if (!AisValidator.IsValidPosition(pr.Latitude, pr.Longitude))
        {
            await repo.InsertDeadLetterAsync(rawPayload, $"bad position {pr.Latitude}/{pr.Longitude}");
            _rejected++;
            _logger.LogWarning("REJECT: bad position Lat={Lat} Lon={Lon}", pr.Latitude, pr.Longitude);
            return;
        }

        if (!AisValidator.TryParseTimestamp(msg.MetaData.TimeUtc, out var ts))
        {
            await repo.InsertDeadLetterAsync(rawPayload, $"unparseable timestamp '{msg.MetaData.TimeUtc}'");
            _rejected++;
            _logger.LogWarning("REJECT: unparseable timestamp '{Ts}'", msg.MetaData.TimeUtc);
            return;
        }
        if (!AisValidator.IsNotFutureSkewed(ts, _ingestion.FutureSkewMinutes))
        {
            await repo.InsertDeadLetterAsync(rawPayload, $"future timestamp {ts:o}");
            _rejected++;
            _logger.LogWarning("REJECT: future timestamp {Ts}", ts);
            return;
        }

        var sog = AisValidator.NormalizeSog(pr.Sog);
        var cog = AisValidator.NormalizeCog(pr.Cog);
        var heading = AisValidator.NormalizeHeading(pr.TrueHeading);
        var navStatus = AisValidator.NormalizeNavStatus(pr.NavigationalStatus);
        var rateOfTurn = AisValidator.NormalizeRateOfTurn(pr.RateOfTurn);

        await repo.UpsertVesselAsync(new VesselRecord
        {
            Mmsi = pr.UserID,
            TimestampUtc = ts
        });

        await repo.InsertPositionAsync(new PositionRecord
        {
            Mmsi = pr.UserID,
            Latitude = pr.Latitude,
            Longitude = pr.Longitude,
            Sog = sog,
            Cog = cog,
            TrueHeading = heading,
            NavStatus = navStatus,
            RateOfTurn = rateOfTurn,
            PositionAccuracy = pr.PositionAccuracy,
            MsgTimestampUtc = ts
        });
        _stored++;
        _logger.LogInformation("STORED: MMSI={Mmsi} Lat={Lat} Lon={Lon}", pr.UserID, pr.Latitude, pr.Longitude);
    }

    private async Task HandleStaticData(AisMessage msg, string rawPayload)
    {
        var sd = msg.Message.ShipStaticData;
        if (sd is null) return;

        int? imo = AisValidator.IsValidImo(sd.ImoNumber) ? sd.ImoNumber : null;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAisRepository>();

       await repo.UpsertVesselAsync(new VesselRecord
{
    Mmsi = sd.UserID,
    Imo = imo,
    Name = sd.Name,
    CallSign = sd.CallSign,
    ShipType = sd.Type,
    DimToBow = (short?)sd.Dimension?.A,
    DimToStern = (short?)sd.Dimension?.B,
    DimToPort = (short?)sd.Dimension?.C,
    DimToStarboard = (short?)sd.Dimension?.D,
    Draught = sd.MaximumStaticDraught > 0 ? (decimal)sd.MaximumStaticDraught : null,
    Destination = string.IsNullOrWhiteSpace(sd.Destination) ? null : sd.Destination.Trim(),
    Eta = BuildEta(sd.Eta),
    TimestampUtc = DateTime.UtcNow
});
        _staticStored++;
        _logger.LogInformation("STATIC STORED: MMSI={Mmsi} IMO={Imo} Name={Name}", sd.UserID, imo, sd.Name);
    }
    private async Task RunSummaryLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Summary loop started (every {Sec}s).", _ingestion.SummaryIntervalSeconds);

        try
        {
            var interval = TimeSpan.FromSeconds(_ingestion.SummaryIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken);

                _logger.LogInformation(
                    "SUMMARYyyyyyyyy!!!!!!!! (last {Seconds}s): received={Received} stored={Stored} rejected={Rejected} staticStored={Static} reconnects={Reconnects}",
                    _ingestion.SummaryIntervalSeconds,
                    Interlocked.Exchange(ref _received, 0),
                    Interlocked.Exchange(ref _stored, 0),
                    Interlocked.Exchange(ref _rejected, 0),
                    Interlocked.Exchange(ref _staticStored, 0),
                    Interlocked.Exchange(ref _reconnects, 0));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Summary loop crashed!");
        }
    }
private static DateTime? BuildEta(Eta? eta)
{
    if (eta is null) return null;
    if (eta.Month is < 1 or > 12 || eta.Day is < 1 or > 31) return null;
    if (eta.Hour > 23 || eta.Minute > 59) return null;

    var year = DateTime.UtcNow.Year;
    try
    {
        return new DateTime(year, eta.Month, eta.Day, eta.Hour, eta.Minute, 0, DateTimeKind.Utc);
    }
    catch (ArgumentOutOfRangeException)
    {
        return null;
    }
}
}
