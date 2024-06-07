using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618

namespace Abm.Pyro.Domain.Model;

public class HttpVerb : DbBase
{
  private HttpVerb(){}
  
  public HttpVerb(HttpVerbId httpVerbId, string name)
  {
    HttpVerbId = httpVerbId;
    Name = name;
  }
  public HttpVerbId HttpVerbId { get; set; }
  public string  Name { get; set; }
}
