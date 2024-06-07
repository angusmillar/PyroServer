using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class SearchModifierCode : DbBase
{
  private SearchModifierCode() { }
  public SearchModifierCode(SearchModifierCodeId searchModifierCodeId, string name)
  {
    SearchModifierCodeId = searchModifierCodeId;
    Name = name;
  }
  
  public SearchModifierCodeId SearchModifierCodeId { get; set; }
  public string Name { get; set; }
}
