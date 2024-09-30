using System;
using System.Collections.Generic;
namespace Abm.Pyro.CodeGeneration.SearchParameters
{

  public class SearchParameterDto
  {

    public SearchParameterDto(
      int searchParameterStoreId,
      string resourceId,
      int versionId,
      bool isCurrent,
      bool isDeleted,
      string name,
      string status,
      string url, string code,
      string type,
      string expression,
      string multipleOr,
      string multipleAnd,
      string chain,
      string json,
      string lastUpdated
    )
    {
      SearchParameterStoreId = searchParameterStoreId;
      ResourceId = resourceId;
      VersionId = versionId;
      IsCurrent = isCurrent;
      IsDeleted = isDeleted;
      Name = name;
      Status = status;
      Url = url;
      Code = code;
      Type = type;
      Expression = expression;
      MultipleOr = multipleOr;
      MultipleAnd = multipleAnd;
      Chain = chain;
      Json = json;
      LastUpdated = lastUpdated;
    }
    public int SearchParameterStoreId { get; set; }

    public string ResourceId { get; set; }

    public int VersionId { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; }

    public string Status { get; set; }

    public string Url { get; set; }

    public string Code { get; set; }

    public string Type { get; set; }
    
    public string Expression { get; set; }

    public string MultipleOr { get; set; }

    public string MultipleAnd { get; set; }
    
    public string Chain { get; set; }

    public string Json { get; set; }
    
    public string LastUpdated { get; set; }
    
  }

}