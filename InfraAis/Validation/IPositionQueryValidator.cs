using InfraAis.Models;

namespace InfraAis.Validation;

public interface IPositionQueryValidator
{
    public ValidationResult<PositionQueryFilters> Validate(PositionQuery query);
}