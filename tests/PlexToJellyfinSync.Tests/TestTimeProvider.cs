namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Time provider whose current time can be advanced manually
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    #region Fields

    private DateTimeOffset _now;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="start">Initial time</param>
    public TestTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Advance the current time by a span
    /// </summary>
    /// <param name="delta">Amount of time to advance</param>
    public void Advance(TimeSpan delta)
    {
        _now = _now.Add(delta);
    }

    #endregion // Methods

    #region TimeProvider

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    #endregion // TimeProvider
}