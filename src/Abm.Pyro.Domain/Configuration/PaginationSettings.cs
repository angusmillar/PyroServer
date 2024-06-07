using System.ComponentModel.DataAnnotations;
namespace Abm.Pyro.Domain.Configuration;

public sealed class PaginationSettings
{
  public const string SectionName = "Pagination";
  /// <summary>
  /// This setting is the default number of Resources returned in a bundle, for example,
  /// in a FHIR search call. The default can be over-ridden per API call using the _count search parameter
  /// in the call.
  /// </summary>
  [Range(1, 200, ErrorMessage = "Can only be between 1 .. 200")]
  public int DefaultNumberOfRecordsPerPage { get; init; } = 30;
  
  /// <summary>
  /// This setting is the absolute maximum number of Resource that can be requested 
  /// when using the _count search parameter. For example, if an API caller sets the parameter _count=500 
  /// and this command 'MaxNumberOfRecordsPerPage' is set to 200 then the _count search parameter value 
  /// will be ignored and only 200 will be returned. This is to prevent users asking for a _count value 
  /// that is too large, resulting in poor performance of the service. Also, beware that the service has 
  /// an internal setting called 'SystemDefaultMaxNumberOfRecordsPerPage' that can not be changed by 
  /// configuration. This command 'MaxNumberOfRecordsPerPage' cannot exceed the 
  /// 'SystemDefaultMaxNumberOfRecordsPerPage' command which is currently set at 5000 and the command 
  ///  here will default to the 'SystemDefaultMaxNumberOfRecordsPerPage' value if set higher.
  /// </summary>
  [Range(1, 300, ErrorMessage = "Can only be between 1 .. 300")]
  public int MaximumNumberOfRecordsPerPage { get; init; } = 100;
  
}


