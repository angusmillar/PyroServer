namespace Abm.Pyro.Domain.Support;

public static class DecimalSupport
{
  public static DecimalInfo GetDecimalInfo(decimal dec)
  {
    var x = new System.Data.SqlTypes.SqlDecimal(dec);
    return new DecimalInfo((int)x.Precision, (int)x.Scale);
  }

  public static decimal CalculateHighNumber(decimal value, int scale)
  {
    return Decimal.Add(value, CalculateNewScale(scale));
  }

  public static decimal CalculateLowNumber(decimal value, int scale)
  {
    return Decimal.Subtract(value, CalculateNewScale(scale));
  }

  private static decimal CalculateNewScale(int scale)
  {
    const double margin = 5;
    return Convert.ToDecimal(margin / (Math.Pow(10, scale + 1)));
  }

}
