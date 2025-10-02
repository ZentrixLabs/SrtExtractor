namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for running external processes and capturing their output.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Run an external process and capture its output.
    /// </summary>
    /// <param name="exe">Path to the executable</param>
    /// <param name="args">Command line arguments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Exit code, stdout, and stderr</returns>
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string args, 
        CancellationToken ct = default);

    /// <summary>
    /// Run an external process with a custom timeout and capture its output.
    /// </summary>
    /// <param name="exe">Path to the executable</param>
    /// <param name="args">Command line arguments</param>
    /// <param name="timeout">Custom timeout duration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Exit code, stdout, and stderr</returns>
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string args, 
        TimeSpan timeout,
        CancellationToken ct = default);
}
