namespace HyperFrame.Core
{
    /// <summary>Raised when the app goes to the background (Paused = true) or returns.</summary>
    public struct AppPauseEvent { public bool Paused; }

    /// <summary>Raised once when the application is quitting.</summary>
    public struct AppQuitEvent { }

    /// <summary>Raised when the game clock is paused or resumed (pause popup, debug console...).</summary>
    public struct GamePausedEvent { public bool Paused; }
}
