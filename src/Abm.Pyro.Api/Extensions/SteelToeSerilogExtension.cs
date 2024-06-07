using Serilog;
using Serilog.Extensions.Logging;

namespace Abm.Pyro.Api.Extensions;
public static class SteelToeSerilogExtension
{
  /// <summary>
  /// Used to provide an ILoggerFactory where required, for instance to allow 
  /// the SteelToe Configuration Provider to log what it is doing 
  /// </summary>
  /// <returns></returns>
  public static ILoggerFactory GetLoggerFactory()
  {
    var loggerFactory = new LoggerFactory();
    loggerFactory.AddProvider(new SerilogLoggerProvider(Log.Logger, true));
    return loggerFactory;
  }

}
