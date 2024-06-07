using System.ComponentModel.DataAnnotations;
namespace Abm.Pyro.Domain.Configuration;

public sealed class IncludeRevIncludeSettings
{
  public const string SectionName = "IncludeRevInclude";
  
  /// <summary>
  /// This setting controls the maximum number of iterations that the server will perform for the set of 
  /// _include or _revinclude search parameters in a single search query where the 'iterate' or 'recurse'
  /// modifiers are in use.
  /// (e.g. [base]/Observation?_id=obs-1&amp;_include:iterate=Observation:has-member 
  /// </summary>
  [Range(1, 200, ErrorMessage = "Can only be between 1 .. 200")]
  public int MaximumIterations { get; init; } = 10;
  
  /// <summary>
  /// This setting controls the maximum number of _include or _revinclude resources that the server will
  /// return in a single search query. The initial target resources are not part of this count, only the
  /// resource which are added by the _include or _revinclude search parameters.  
  /// </summary>
  [Range(1, 200, ErrorMessage = "Can only be between 1 .. 200")]
  public int MaximumIncludeResources { get; init; } = 100;
}
