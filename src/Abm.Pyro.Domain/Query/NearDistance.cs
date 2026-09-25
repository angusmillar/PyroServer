using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Domain.Query;

/// <summary>
/// How far a matched Location is from the point a client searched near, and the unit the client
/// expressed their search in, so the result can be reported back in the same unit.
/// </summary>
public record NearDistance(double Metres, NearDistanceUnit ReportUnit);
