using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class Comparator : DbBase
{
  private Comparator() { }
  public Comparator(SearchComparatorId searchComparatorId, string name)
  {
    SearchComparatorId = searchComparatorId;
    Name = name;
  }
  
  public SearchComparatorId SearchComparatorId { get; set; }
  public string Name { get; set; }
}
