namespace ObfusCal.Application.Interfaces;

/// <summary>
/// A progress snapshot reported during a sync cycle.
/// </summary>
public sealed record SyncProgressUpdate(string Message, int Current, int Total)
{
    // True when Total is zero or unknown - the caller should render an indeterminate bar.
    public bool IsIndeterminate => Total <= 0;

    // Returns null when indeterminate; otherwise 0–100.
    public int? PercentComplete => IsIndeterminate ? null : (int)(Current * 100L / Total);
}
