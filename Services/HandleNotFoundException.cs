namespace CfMvc.Services;

/// <summary>
/// Thrown when the Codeforces API returns a 400 for a user.status request,
/// indicating the handle does not exist. Caught separately from fatal errors
/// so the contest table can still be rendered in no-handle mode.
/// </summary>
public sealed class HandleNotFoundException(string handle)
    : Exception($"Handle \"{handle}\" was not found on Codeforces.")
{
    public string Handle { get; } = handle;
}
