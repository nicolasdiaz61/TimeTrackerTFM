namespace TimeTracker.Api.Configuration;

public sealed class ApiCorsOptions
{
    public const string SectionName = "Cors";

    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];
}
