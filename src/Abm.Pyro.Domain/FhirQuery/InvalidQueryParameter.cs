namespace Abm.Pyro.Domain.FhirQuery;

public class InvalidQueryParameter
{
  public string Name { get; set; }
  public string Value { get; set; }
  public string ErrorMessage { get; set; }
  public InvalidQueryParameter(string name, string value, string errorMessage)
  {
    Name = name;
    Value = value;
    ErrorMessage = errorMessage;
  }
  public InvalidQueryParameter(string rawParameter, string errorMessage)
  {
    string[] spit = rawParameter.Split('=');
    Name = spit[0];
    Value = spit.Length > 1 ? spit[1] : String.Empty;
    ErrorMessage = errorMessage;
  }
}
