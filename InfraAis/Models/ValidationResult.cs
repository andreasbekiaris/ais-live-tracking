namespace InfraAis.Models;

public class ValidationResult<T>
{
    public bool IsValid { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
}