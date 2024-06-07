namespace Abm.Pyro.Domain.Configuration;

public sealed class DatabaseSettings
{
  public const string SectionName = "Database";
  public string ConnectionString { get; init; } = String.Empty;
}
