using System.Diagnostics;
using System.Text;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for running external processes and capturing their output.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private readonly ILoggingService _loggingService;

    public ProcessRunner(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string args, 
        CancellationToken ct = default)
    {
        _loggingService.LogInfo($"Running process: {exe} {args}");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to complete or cancellation with timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5 minute timeout
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                _loggingService.LogWarning($"Process timed out after 5 minutes: {exe} {args}");
                process.Kill();
                throw new TimeoutException($"Process timed out: {exe}");
            }

            var stdout = outputBuilder.ToString().Trim();
            var stderr = errorBuilder.ToString().Trim();

            _loggingService.LogInfo($"Process completed with exit code: {process.ExitCode}");
            
            if (!string.IsNullOrEmpty(stdout))
            {
                _loggingService.LogInfo($"Process output: {stdout}");
            }
            
            if (!string.IsNullOrEmpty(stderr))
            {
                _loggingService.LogWarning($"Process error output: {stderr}");
            }

            return (process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning("Process execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to run process: {exe} {args}", ex);
            throw;
        }
    }
}
