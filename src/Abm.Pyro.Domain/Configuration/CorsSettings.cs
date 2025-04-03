namespace Abm.Pyro.Domain.Configuration;

public class CorsSettings
{
    public const string SectionName = "Cors";
    
    public required string[] AllowedOriginsList { get; init; }
    
}