namespace Abm.Pyro.Domain.Model;

public static class RepositoryModelConstraints
{
  public const int FhirResourceNameMaxLength = 256;
  public const int FhirIdMaxLength = 64;
  public const int TimestampPrecision = 3;
  public const int CodeMaxLength = 100;
  public const int StringMaxLength = 450;
  public const int QuantityPrecision =  18;
  public const int QuantityScale =  6;
  public const string CaseSensitive = "SQL_Latin1_General_CP1_CS_AS";
  public const string CaseInsensitive  = "SQL_Latin1_General_CP1_CI_AS";

}
