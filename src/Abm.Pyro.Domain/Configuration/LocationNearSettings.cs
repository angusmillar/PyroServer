using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public sealed class LocationNearSettings : IValidatableObject
{
  public const string SectionName = "LocationNear";

  /// <summary>
  /// Half the earth's circumference; the largest meaningful great-circle distance.
  /// </summary>
  private const int MaximumSupportedDistanceInMetres = 20_000_000;

  /// <summary>
  /// The search radius used when a client omits the [distance] segment of a 'near' search
  /// parameter value, for example 'Location?near=-33.8568|151.2153'. FHIR R4 leaves this to
  /// the server's discretion.
  /// </summary>
  [Range(1, MaximumSupportedDistanceInMetres, ErrorMessage = "Can only be between 1 .. 20000000")]
  public int DefaultDistanceInMetres { get; init; } = 10_000;

  /// <summary>
  /// The largest radius a client may request. A request above this is rejected with a 400 so
  /// that a single search cannot scan the whole position index.
  /// </summary>
  [Range(1, MaximumSupportedDistanceInMetres, ErrorMessage = "Can only be between 1 .. 20000000")]
  public int MaximumDistanceInMetres { get; init; } = 1_000_000;

  /// <summary>
  /// When true, each matched Location in a search result carries its distance from the
  /// requested point as a 'location-distance' extension on Bundle.entry.search. This costs one
  /// extra page-scoped database query per near search. Turning it off does not change which
  /// Locations match, only whether the response reports how far away they are.
  /// </summary>
  public bool ReturnDistanceInSearchResults { get; init; } = true;

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (DefaultDistanceInMetres > MaximumDistanceInMetres)
    {
      yield return new ValidationResult(
        $"{nameof(DefaultDistanceInMetres)} ({DefaultDistanceInMetres}) must not be greater than " +
        $"{nameof(MaximumDistanceInMetres)} ({MaximumDistanceInMetres}).",
        new[] { nameof(DefaultDistanceInMetres), nameof(MaximumDistanceInMetres) });
    }
  }
}
