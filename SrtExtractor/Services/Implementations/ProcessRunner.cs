using System.Diagnostics;
using System.IO;
using System.Text;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.Utils;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for running external processes and capturing their output.
/// SECURITY: Uses ArgumentList to prevent command injection vulnerabilities.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private readonly ILoggingService _loggingService;

    public ProcessRunner(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    // NEW SECURE METHODS - Use ArgumentList to prevent command injection

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string[] args, 
        CancellationToken ct = default)
    {
        return await RunAsync(exe, args, TimeSpan.FromHours(2), ct);
    }

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string[] args, 
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        // SECURITY: Validate the executable path
        // If it's a full path, validate it exists and is safe
        // If it's just a name (e.g., "ffmpeg.exe"), allow it (will be resolved via PATH or current directory)
        string validatedExe;
        if (Path.IsPathRooted(exe) || exe.Contains(Path.DirectorySeparatorChar) || exe.Contains(Path.AltDirectorySeparatorChar))
        {
            // Full or relative path - validate it exists and is safe
            validatedExe = PathValidator.ValidateFileExists(exe);
        }
        else
        {
            // Just an executable name - sanitize but don't require it to exist as a file
            // (will be resolved via PATH, current directory, or app directory)
            validatedExe = PathValidator.ValidateAndSanitizeFilePath(exe);
        }
        
        // Log command execution (sanitized for security)
        var sanitizedArgs = string.Join(" ", args.Select(arg => 
            arg.Length > 50 ? $"[arg-{arg.Length}chars]" : $"[arg]"));
        _loggingService.LogInfo($"Running process: {Path.GetFileName(validatedExe)} with {args.Length} arguments");

        Process? process = null;
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? combinedCts = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = validatedExe,
                // SECURITY: Don't use Arguments property - use ArgumentList instead
                // This prevents command injection by not interpreting special characters
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // SECURITY: Add arguments individually to ArgumentList
            // This prevents command injection attacks
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            process = new Process { StartInfo = startInfo };
            
            // Limit StringBuilder capacity to prevent unbounded memory growth (10MB max)
            const int MaxOutputSize = 10 * 1024 * 1024;
            var outputBuilder = new StringBuilder(4096);
            var errorBuilder = new StringBuilder(4096);
            var outputSize = 0L;
            var errorSize = 0L;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    var dataSize = Encoding.UTF8.GetByteCount(e.Data);
                    if (outputSize + dataSize < MaxOutputSize)
                    {
                        outputBuilder.AppendLine(e.Data);
                        outputSize += dataSize;
                    }
                    else
                    {
                        _loggingService.LogWarning("Process output exceeded maximum size limit");
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    var dataSize = Encoding.UTF8.GetByteCount(e.Data);
                    if (errorSize + dataSize < MaxOutputSize)
                    {
                        errorBuilder.AppendLine(e.Data);
                        errorSize += dataSize;
                    }
                    else
                    {
                        _loggingService.LogWarning("Process error output exceeded maximum size limit");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to complete or cancellation with timeout
            timeoutCts = new CancellationTokenSource(timeout);
            combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                _loggingService.LogWarning($"Process timed out after {timeout.TotalMinutes:F0} minutes");
                throw new TimeoutException($"Process timed out: {Path.GetFileName(validatedExe)}");
            }
            catch (OperationCanceledException)
            {
                _loggingService.LogWarning("Process execution was cancelled, killing process");
                throw;
            }

            var stdout = outputBuilder.ToString().Trim();
            var stderr = errorBuilder.ToString().Trim();

            _loggingService.LogInfo($"Process completed with exit code: {process.ExitCode}");
            
            // Don't log full output for security reasons - it may contain sensitive paths
            if (!string.IsNullOrEmpty(stdout))
            {
                _loggingService.LogInfo($"Process output: {stdout.Length} characters");
            }
            
            if (!string.IsNullOrEmpty(stderr))
            {
                _loggingService.LogWarning($"Process error output: {stderr.Length} characters");
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
            _loggingService.LogError($"Failed to run process: {Path.GetFileName(exe)}", ex);
            throw;
        }
        finally
        {
            // Ensure proper cleanup of process and resources
            try
            {
                if (process != null && !process.HasExited)
                {
                    _loggingService.LogInfo("Killing process tree due to timeout or cancellation");
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception killEx)
                    {
                        _loggingService.LogWarning($"Error killing process: {killEx.Message}");
                    }
                }
            }
            finally
            {
                // Dispose all resources
                process?.Dispose();
                timeoutCts?.Dispose();
                combinedCts?.Dispose();
            }
        }
    }

    // DEPRECATED METHODS - Keep for backward compatibility but mark as obsolete

    [Obsolete("Use RunAsync with string[] args to prevent command injection vulnerabilities")]
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string args, 
        CancellationToken ct = default)
    {
        return await RunAsync(exe, args, TimeSpan.FromHours(2), ct);
    }

    [Obsolete("Use RunAsync with string[] args to prevent command injection vulnerabilities")]
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string args, 
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        _loggingService.LogWarning("SECURITY: Using deprecated string-based RunAsync - migrate to string[] args");

        Process? process = null;
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? combinedCts = null;

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

            process = new Process { StartInfo = startInfo };
            
            // Limit StringBuilder capacity to prevent unbounded memory growth (10MB max)
            const int MaxOutputSize = 10 * 1024 * 1024;
            var outputBuilder = new StringBuilder(4096);
            var errorBuilder = new StringBuilder(4096);
            var outputSize = 0L;
            var errorSize = 0L;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    var dataSize = System.Text.Encoding.UTF8.GetByteCount(e.Data);
                    if (outputSize + dataSize < MaxOutputSize)
                    {
                        outputBuilder.AppendLine(e.Data);
                        outputSize += dataSize;
                    }
                    else
                    {
                        _loggingService.LogWarning("Process output exceeded maximum size limit");
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    var dataSize = System.Text.Encoding.UTF8.GetByteCount(e.Data);
                    if (errorSize + dataSize < MaxOutputSize)
                    {
                        errorBuilder.AppendLine(e.Data);
                        errorSize += dataSize;
                    }
                    else
                    {
                        _loggingService.LogWarning("Process error output exceeded maximum size limit");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to complete or cancellation with timeout
            timeoutCts = new CancellationTokenSource(timeout);
            combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                _loggingService.LogWarning($"Process timed out after {timeout.TotalMinutes:F0} minutes: {exe} {args}");
                throw new TimeoutException($"Process timed out: {exe}");
            }
            catch (OperationCanceledException)
            {
                _loggingService.LogWarning("Process execution was cancelled, killing process");
                throw;
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
        finally
        {
            // Ensure proper cleanup of process and resources
            try
            {
                if (process != null && !process.HasExited)
                {
                    _loggingService.LogInfo("Killing process tree due to timeout or cancellation");
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception killEx)
                    {
                        _loggingService.LogWarning($"Error killing process: {killEx.Message}");
                    }
                }
            }
            finally
            {
                // Dispose all resources
                process?.Dispose();
                timeoutCts?.Dispose();
                combinedCts?.Dispose();
            }
        }
    }
}
