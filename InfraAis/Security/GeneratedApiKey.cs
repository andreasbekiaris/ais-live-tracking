public record GeneratedApiKey(
    string RawKey,
    byte[] Hash,
    string Prefix
);