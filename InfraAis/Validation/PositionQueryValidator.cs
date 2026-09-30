using InfraAis.Models;
using InfraAis.Utils;



namespace InfraAis.Validation;



public class PositionQueryValidator : IPositionQueryValidator
{

    public ValidationResult<PositionQueryFilters> Validate(PositionQuery query, bool isFleet = true)
    {



        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'from' must be earlier than or equal to 'to'."
            };
        }
        if (query.Limit.HasValue && query.Limit.Value <= 0)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'limit' must be a positive integer (values above 1000 are clamped to 1000)."
            };
        }
        if (query.Limit.HasValue && query.Limit.Value > 1000)
        {
            query.Limit = 1000;
        }

        if (query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLat.Value > query.MaxLat.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'minLat' must be less than or equal to 'maxLat'."
            };
        }
        if (query.MinLon.HasValue && query.MaxLon.HasValue && query.MinLon.Value > query.MaxLon.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'minLon' must be less than or equal to 'maxLon'."
            };
        }
        if (!(query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLon.HasValue && query.MaxLon.HasValue || !query.MinLat.HasValue && !query.MaxLat.HasValue && !query.MinLon.HasValue && !query.MaxLon.HasValue))
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Bounding box requires all four of minLat, maxLat, minLon, maxLon (or none of them)."
            };

        }
        if (query.MaxLat.HasValue && query.MaxLon.HasValue && AisValidator.IsValidPosition(query.MaxLat.Value, query.MaxLon.Value) == false)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'maxLat' must be between -90 and 90 and 'maxLon' between -180 and 180."
            };
        }

        if (query.MinLat.HasValue && query.MinLon.HasValue && AisValidator.IsValidPosition(query.MinLat.Value, query.MinLon.Value) == false)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'minLat' must be between -90 and 90 and 'minLon' between -180 and 180."
            };
        }

        if (query.MinSog.HasValue && query.MaxSog.HasValue && query.MinSog.Value > query.MaxSog.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'minSog' must be less than or equal to 'maxSog'."
            };
        }
        if (isFleet && query.From.HasValue != query.To.HasValue)
{
    return new ValidationResult<PositionQueryFilters>
    {
        IsValid = false,
        Error = "Both 'from' and 'to' must be provided together."
    };
}
        bool isBoundingBoxProvided = query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLon.HasValue && query.MaxLon.HasValue;
        if (isFleet && !isBoundingBoxProvided && !query.From.HasValue && !query.To.HasValue)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Fleet queries require a time window (from and to) or a bounding box."
            };
        }
        if (query.MaxSog is not null)
        {
            var maxSog = AisValidator.NormalizeSog((double)query.MaxSog);

            if (maxSog is null)
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "'maxSog' must be a non-negative speed in knots (102.3 means 'not available')."
                };
            }
        }

        if (query.MinSog is not null)
        {
            var minSog = AisValidator.NormalizeSog((double)query.MinSog);

            if (minSog is null)
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "'minSog' must be a non-negative speed in knots (102.3 means 'not available')."
                };
            }
        }

        if (!isFleet && query.Mmsi is not null)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "'mmsi' is not allowed on the single-vessel endpoint; the vessel comes from the URL."
            };
        }

        if (query.Mmsi is not null)
        {
            var mmsiParts = query.Mmsi.Split(',').Select(m => m.Trim()).ToList();

            if (mmsiParts.Any(m => !long.TryParse(m, out _)))
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "'mmsi' must be a comma-separated list of 9-digit MMSI numbers."
                };
            }

            var mmsiList = mmsiParts.Select(long.Parse).ToList();

            if (mmsiList.Any(m => !AisValidator.IsValidMmsi(m)))
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "'mmsi' must be a comma-separated list of 9-digit MMSI numbers."
                };
            }
        }
        if (query.NavStatus is not null)
{
    var navStatusParts = query.NavStatus
        .Split(',')
        .Select(n => n.Trim())
        .ToList();

    if (navStatusParts.Any(n => !int.TryParse(n, out _)))
    {
        return new ValidationResult<PositionQueryFilters>
        {
            IsValid = false,
            Error = "Navigation status values must be integers."
        };
    }

    var navStatusList = navStatusParts
        .Select(int.Parse)
        .ToList();

    if (navStatusList.Any(n => AisValidator.NormalizeNavStatus(n) is null))
    {
        return new ValidationResult<PositionQueryFilters>
        {
            IsValid = false,
            Error = "One or more navigation status values are outside the valid AIS range."
        };
    }
}
       var normalizedSort = query.Sort?.Trim().ToLowerInvariant();

if (normalizedSort is not null &&
    normalizedSort != "asc" &&
    normalizedSort != "desc")
{
    return new ValidationResult<PositionQueryFilters>
    {
        IsValid = false,
        Error = "'sort' must be either 'asc' or 'desc'."
    };
}
        if (query.Cursor is not null)
        {
            if (CursorCodec.Decode(query.Cursor) is null)
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "'cursor' is invalid or corrupted; use the nextCursor value from a previous response."
                };
            }
        }
        return new ValidationResult<PositionQueryFilters>
        {
            IsValid = true,
            Value = new PositionQueryFilters
            {
                BoundingBox = query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLon.HasValue && query.MaxLon.HasValue
                    ? new BoundingBox
                    {
                        MinLat = query.MinLat.Value,
                        MaxLat = query.MaxLat.Value,
                        MinLon = query.MinLon.Value,
                        MaxLon = query.MaxLon.Value
                    }
                    : null,
                MaxSog = query.MaxSog,
                MinSog = query.MinSog,
                Limit = query.Limit.GetValueOrDefault(100),
                From = query.From,
                To = query.To,
                Mmsis = query.Mmsi?.Split(',').Select(m => m.Trim()).Where(m => long.TryParse(m, out _)).Select(long.Parse).ToList() ?? new List<long>(),
                NavStatuses = query.NavStatus?.Split(',').Select(n => n.Trim()).Where(n => int.TryParse(n, out _)).Select(int.Parse).ToList() ?? new List<int>(),
               Sort = normalizedSort == "asc"
    ? SortDirection.Asc
    : SortDirection.Desc,
                Cursor = query.Cursor
            }
        };
    }
}