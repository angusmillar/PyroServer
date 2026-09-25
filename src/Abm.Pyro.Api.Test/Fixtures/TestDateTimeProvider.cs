using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Api.Test.Fixtures;

/// <summary>
/// Replaces the server's <see cref="IDateTimeProvider"/> so a test can pin the exact
/// instant the server stamps on <c>Meta.LastUpdated</c> when a resource is committed.
///
/// Unset (<see cref="FrozenNow"/> is null) it simply defers to the real clock, so every
/// test that does not care about time behaves exactly as before.
/// </summary>
public class TestDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// When set, every server request reports this instant as "now". Null restores the real clock.
    /// </summary>
    public DateTimeOffset? FrozenNow { get; set; }

    public DateTimeOffset Now => FrozenNow ?? DateTimeOffset.Now;

    public void FreezeAt(DateTimeOffset instant) => FrozenNow = instant;

    public void Unfreeze() => FrozenNow = null;
}
