namespace Abm.Pyro.Domain.Support;

public class DecimalInfo(int precision, int scale)
{
  public int Precision { get; private set; } = precision;
  public int Scale { get; private set; } = scale;
}