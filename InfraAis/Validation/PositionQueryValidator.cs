using InfraAis.Models;
using InfraAis.Utils;



namespace InfraAis.Validation;



public class PositionQueryValidator : IPositionQueryValidator
{

    public ValidationResult<PositionQueryFilters> Validate(PositionQuery query)
    {
        
        
      
if(query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid date range"
            };
        }
if(query.Limit.HasValue && query.Limit.Value <= 0 )
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid limit"
            };
        }
        if(query.Limit.HasValue && query.Limit.Value > 1000)
        {
            query.Limit = 1000;
        }
       
        if(query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLat.Value > query.MaxLat.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid latitude range"
            };
        }
        if(query.MinLon.HasValue && query.MaxLon.HasValue && query.MinLon.Value > query.MaxLon.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid longitude range"
            };
        }
        if(!(query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLon.HasValue && query.MaxLon.HasValue || !query.MinLat.HasValue && !query.MaxLat.HasValue && !query.MinLon.HasValue && !query.MaxLon.HasValue))
        {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "Invalid bounding box"
                };

        }
        if (query.MaxLat.HasValue && query.MaxLon.HasValue && AisValidator.IsValidPosition(query.MaxLat.Value, query.MaxLon.Value) == false)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid position"
            };
        }
       
        if (query.MinLat.HasValue && query.MinLon.HasValue && AisValidator.IsValidPosition(query.MinLat.Value, query.MinLon.Value) == false)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid position"
            };
        }
     
         if(query.MinSog.HasValue && query.MaxSog.HasValue && query.MinSog.Value > query.MaxSog.Value)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid speed range"
            };
        }
         bool isBoundingBoxProvided = query.MinLat.HasValue && query.MaxLat.HasValue && query.MinLon.HasValue && query.MaxLon.HasValue;
        if(!isBoundingBoxProvided && !query.From.HasValue && !query.To.HasValue)
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Bounding box or date window is required"
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
            Error = "Invalid MaxSog value"
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
            Error = "Invalid MinSog value"
        };
    }
}

if (query.Mmsi is not null)
{
    var mmsiParts = query.Mmsi.Split(',').Select(m => m.Trim()).ToList();

    if (mmsiParts.Any(m => !long.TryParse(m, out _)))
    {
        return new ValidationResult<PositionQueryFilters>
        {
            IsValid = false,
            Error = "Invalid MMSI values"
        };
    }

    var mmsiList = mmsiParts.Select(long.Parse).ToList();

    if (mmsiList.Any(m => !AisValidator.IsValidMmsi(m)))
    {
        return new ValidationResult<PositionQueryFilters>
        {
            IsValid = false,
            Error = "Invalid MMSI values"
        };
    }
}
if(query.NavStatus is not null)
        {
            var navStatusParts = query.NavStatus.Split(',').Select(n => n.Trim()).ToList();
            var navStatusList = navStatusParts.Where(n => int.TryParse(n, out _)).Select(int.Parse).ToList();
            if (navStatusList.Any(n => AisValidator.NormalizeNavStatus(n) is null))
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "Invalid navigation status values"
                };
            }
        }
      if(query.Sort is not null && query.Sort != "asc" && query.Sort != "desc")
        {
            return new ValidationResult<PositionQueryFilters>
            {
                IsValid = false,
                Error = "Invalid sort value"
            };
        }
if(query.Cursor is not null)
        {
            if(CursorCodec.Decode(query.Cursor) is null)
            {
                return new ValidationResult<PositionQueryFilters>
                {
                    IsValid = false,
                    Error = "Invalid cursor value"
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
            Sort = query.Sort == "asc" ? SortDirection.Asc : SortDirection.Desc,
              Cursor = query.Cursor is not null ? CursorCodec.Decode(query.Cursor) : null
                }
          };
    } 
}