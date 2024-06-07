using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class PublicationStatus : DbBase
{
  private PublicationStatus() {
  }
  
  public PublicationStatus(PublicationStatusId publicationStatusId, string name)
  {
    PublicationStatusId = publicationStatusId;
    Name = name;
  }
  
  public PublicationStatusId PublicationStatusId { get; set; }

  public string Name { get; set; }
}
