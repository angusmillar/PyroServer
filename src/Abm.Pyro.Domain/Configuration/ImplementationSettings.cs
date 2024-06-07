using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public class ImplementationSettings
{
    public const string SectionName = "Implementation";
   
    [Required(AllowEmptyStrings=false)]
    public required string Name { get; init; }
    
    public string? Title { get; init; }
    
    public string? Description { get; init; }
    
}