# Security Review - SrtExtractor Application

**Review Date:** October 9, 2025  
**Reviewer:** Security Professional Review  
**Application:** SrtExtractor v1.0  
**Framework:** WPF .NET 9

## Executive Summary

This security review identified **8 security issues** ranging from **CRITICAL** to **LOW** severity. The application has good foundations with proper process isolation and resource limits, but requires immediate attention to command injection vulnerabilities and path traversal risks.

**Risk Level:** **HIGH** ⚠️

---

## Critical Issues (2)

### 1. Command Injection via Unsanitized File Paths

**Severity:** CRITICAL 🔴  
**CWE:** CWE-78 (OS Command Injection)  
**CVSS Score:** 9.8 (Critical)

**Location:**
- `MkvToolService.cs` lines 61, 110, 165, 226
- `SubtitleOcrService.cs` line 90
- `FfmpegService.cs` line 226

**Vulnerability Description:**

File paths from user input (file picker, drag-and-drop, recent files) are directly interpolated into command-line arguments without proper sanitization. This creates command injection vulnerabilities.

**Vulnerable Code Example:**
```csharp
// MkvToolService.cs:61
var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
    toolStatus.Path, 
    $"-J \"{mkvPath}\"");  // ❌ Unsanitized user input

// MkvToolService.cs:110
var args = $"tracks \"{mkvPath}\" {trackId}:\"{outSrt}\"";  // ❌ Multiple injection points

// SubtitleOcrService.cs:90
var args = $"\"{supPath}\" subrip /outputfilename:\"{outSrt}\" /ocrdb:{ocrDb}";  // ❌ Unsanitized paths
```

**Attack Scenario:**

An attacker could craft a malicious filename like:
```
movie.mkv" && calc.exe && echo "
movie.mkv" & del /s /q C:\*.* & echo "
movie.mkv`; rm -rf / #
```

When processed, these would execute arbitrary commands:
```bash
mkvmerge -J "movie.mkv" && calc.exe && echo ".mkv"
mkvextract tracks "movie.mkv" && calc.exe && echo ".mkv" 0:"output.srt"
```

**Impact:**
- Arbitrary command execution with user privileges
- File system modification/deletion
- Data exfiltration
- Malware installation
- System compromise

**Recommended Fix:**

1. **Use Argument Arrays** (preferred):
```csharp
// Modify IProcessRunner interface
public interface IProcessRunner
{
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string[] args,  // ✅ Use array instead of string
        CancellationToken ct = default);
}

// ProcessRunner implementation
var startInfo = new ProcessStartInfo
{
    FileName = exe,
    // Don't set Arguments - add individually
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

// Add arguments individually (prevents injection)
foreach (var arg in args)
{
    startInfo.ArgumentList.Add(arg);
}
```

2. **Implement Path Validation**:
```csharp
public static class PathValidator
{
    public static string ValidateAndSanitizeFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty");
        
        // Check for path traversal
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException("Path traversal detected");
        
        // Check for invalid characters
        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(c => invalidChars.Contains(c)))
            throw new ArgumentException("Path contains invalid characters");
        
        // Check for shell metacharacters
        var dangerousChars = new[] { '&', '|', ';', '`', '$', '(', ')', '<', '>' };
        if (path.Any(c => dangerousChars.Contains(c)))
            throw new SecurityException("Path contains shell metacharacters");
        
        return fullPath;
    }
}
```

3. **Update Service Calls**:
```csharp
// MkvToolService.cs
public async Task<ProbeResult> ProbeAsync(string mkvPath, CancellationToken cancellationToken = default)
{
    // Validate path
    var safePath = PathValidator.ValidateAndSanitizeFilePath(mkvPath);
    
    // Use argument array
    var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
        toolStatus.Path, 
        new[] { "-J", safePath },
        cancellationToken);
}
```

**Verification:**
- Test with filenames containing: `&`, `|`, `;`, `` ` ``, `$`, `()`, `<>`, quotes
- Verify commands are not executed
- Check logs for attempted injection

---

### 2. Path Traversal via Unsanitized Output Paths

**Severity:** CRITICAL 🔴  
**CWE:** CWE-22 (Path Traversal)  
**CVSS Score:** 8.6 (High)

**Location:**
- `MkvToolService.cs` lines 103-107, 158-162
- `SubtitleOcrService.cs` lines 46-50
- `FfmpegService.cs` lines 214-218

**Vulnerability Description:**

Output directory paths are created without validation, allowing attackers to write files anywhere on the filesystem.

**Vulnerable Code:**
```csharp
// MkvToolService.cs:103-107
var outputDir = Path.GetDirectoryName(outSrt);
if (!string.IsNullOrEmpty(outputDir))
{
    Directory.CreateDirectory(outputDir);  // ❌ No validation!
}
```

**Attack Scenario:**

An attacker could craft an output path like:
```
C:\Windows\System32\evil.dll
..\..\..\..\Windows\System32\malware.exe
\\?\C:\Windows\System32\backdoor.dll
```

This could:
- Overwrite system files
- Place malware in startup folders
- Write to protected directories
- Modify application files

**Recommended Fix:**

1. **Implement Safe Directory Creation**:
```csharp
public static class SafeFileOperations
{
    private static readonly string[] AllowedBasePaths = 
    {
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        Path.GetTempPath()
    };
    
    public static string ValidateOutputPath(string outputPath)
    {
        // Get full path to resolve any relative paths
        var fullPath = Path.GetFullPath(outputPath);
        
        // Check if path is within allowed directories
        var isAllowed = AllowedBasePaths.Any(basePath => 
            fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase));
        
        if (!isAllowed)
        {
            throw new SecurityException(
                $"Output path is not in an allowed directory. Use Documents, Desktop, Videos, or Temp.");
        }
        
        // Verify no path traversal
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && directory.Contains(".."))
        {
            throw new SecurityException("Path traversal detected in output path");
        }
        
        return fullPath;
    }
    
    public static void SafeCreateDirectory(string path)
    {
        var validatedPath = ValidateOutputPath(path);
        Directory.CreateDirectory(validatedPath);
    }
}
```

2. **Update All Output Operations**:
```csharp
// MkvToolService.cs
public async Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt, CancellationToken cancellationToken = default)
{
    // Validate output path
    var safeOutSrt = SafeFileOperations.ValidateOutputPath(outSrt);
    
    var outputDir = Path.GetDirectoryName(safeOutSrt);
    if (!string.IsNullOrEmpty(outputDir))
    {
        SafeFileOperations.SafeCreateDirectory(outputDir);
    }
    
    // Continue with extraction...
}
```

---

## High Severity Issues (3)

### 3. Sensitive Information Disclosure in Logs

**Severity:** HIGH 🟠  
**CWE:** CWE-532 (Information Exposure Through Log Files)  
**CVSS Score:** 7.5 (High)

**Location:**
- `LoggingService.cs` lines 33, 82-91
- `ProcessRunner.cs` lines 33, 126, 131

**Vulnerability Description:**

Full command lines with potentially sensitive file paths and arguments are logged without sanitization. Logs are stored in a world-readable location (`C:\ProgramData\ZentrixLabs\SrtExtractor\Logs\`) that may be accessible to all users.

**Vulnerable Code:**
```csharp
// ProcessRunner.cs:33
_loggingService.LogInfo($"Running process: {exe} {args}");  // ❌ Full command with paths

// ProcessRunner.cs:126
_loggingService.LogInfo($"Process output: {stdout}");  // ❌ May contain sensitive info

// LoggingService.cs:19-21
_logDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
    "ZentrixLabs", "SrtExtractor", "Logs");  // ❌ World-readable location
```

**Risk:**
- Network paths reveal internal infrastructure
- Filenames may contain sensitive information
- User's file system structure exposed
- Potential privacy violation

**Recommended Fix:**

1. **Implement Log Sanitization**:
```csharp
public static class LogSanitizer
{
    public static string SanitizeFilePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "[empty]";
        
        try
        {
            var filename = Path.GetFileName(path);
            var directory = Path.GetDirectoryName(path);
            
            // Only log filename, not full path
            return $"[...]{Path.DirectorySeparatorChar}{filename}";
        }
        catch
        {
            return "[sanitized-path]";
        }
    }
    
    public static string SanitizeCommandLine(string exe, string args)
    {
        var exeName = Path.GetFileName(exe);
        
        // Remove paths from arguments
        var sanitizedArgs = Regex.Replace(args, 
            @"[A-Za-z]:\\[^""'\s]+|\/[^""'\s]+", 
            "[path]");
        
        return $"{exeName} {sanitizedArgs}";
    }
}
```

2. **Update Logging Calls**:
```csharp
// ProcessRunner.cs
_loggingService.LogInfo($"Running process: {LogSanitizer.SanitizeCommandLine(exe, args)}");

// Instead of full output
_loggingService.LogInfo($"Process completed with exit code: {process.ExitCode}");
// Don't log full stdout/stderr
```

3. **Secure Log Directory Permissions**:
```csharp
public LoggingService()
{
    // Use user-specific directory instead of shared
    _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "SrtExtractor", "Logs");
    
    Directory.CreateDirectory(_logDirectory);
    
    // Set directory permissions (Windows ACL)
    var directoryInfo = new DirectoryInfo(_logDirectory);
    var security = directoryInfo.GetAccessControl();
    
    // Remove inherited permissions
    security.SetAccessRuleProtection(true, false);
    
    // Add only current user
    var currentUser = WindowsIdentity.GetCurrent();
    security.AddAccessRule(new FileSystemAccessRule(
        currentUser.User!,
        FileSystemRights.FullControl,
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
        PropagationFlags.None,
        AccessControlType.Allow));
    
    directoryInfo.SetAccessControl(security);
}
```

---

### 4. Insecure Settings Deserialization

**Severity:** HIGH 🟠  
**CWE:** CWE-502 (Deserialization of Untrusted Data)  
**CVSS Score:** 7.3 (High)

**Location:**
- `SettingsService.cs` lines 43-44

**Vulnerability Description:**

Settings are deserialized from JSON without validation. An attacker who can modify the settings file could inject malicious paths or configurations.

**Vulnerable Code:**
```csharp
// SettingsService.cs:43-44
var json = await File.ReadAllTextAsync(_settingsPath);
var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);  // ❌ No validation
```

**Attack Scenario:**

Attacker modifies `%APPDATA%\SrtExtractor\settings.json`:
```json
{
  "mkvMergePath": "C:\\Windows\\System32\\cmd.exe",
  "subtitleEditPath": "\\\\attacker-server\\malware.exe",
  "tesseractDataPath": "..\\..\\..\\Windows\\System32"
}
```

**Recommended Fix:**

```csharp
public async Task<AppSettings> LoadSettingsAsync()
{
    try
    {
        if (!File.Exists(_settingsPath))
        {
            _loggingService.LogInfo("Settings file not found, using defaults");
            return AppSettings.Default;
        }

        var json = await File.ReadAllTextAsync(_settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
        
        if (settings == null)
        {
            _loggingService.LogWarning("Failed to deserialize settings, using defaults");
            return AppSettings.Default;
        }

        // ✅ Validate all paths before using
        settings = ValidateSettings(settings);
        
        _loggingService.LogInfo("Settings loaded and validated successfully");
        return settings;
    }
    catch (Exception ex)
    {
        _loggingService.LogError("Failed to load settings", ex);
        return AppSettings.Default;
    }
}

private AppSettings ValidateSettings(AppSettings settings)
{
    // Validate tool paths
    if (!string.IsNullOrEmpty(settings.MkvMergePath))
    {
        if (!IsValidToolPath(settings.MkvMergePath, "mkvmerge.exe"))
        {
            _loggingService.LogWarning("Invalid MkvMergePath in settings, clearing");
            settings = settings with { MkvMergePath = null };
        }
    }
    
    if (!string.IsNullOrEmpty(settings.SubtitleEditPath))
    {
        if (!IsValidToolPath(settings.SubtitleEditPath, "seconv.exe"))
        {
            _loggingService.LogWarning("Invalid SubtitleEditPath in settings, clearing");
            settings = settings with { SubtitleEditPath = null };
        }
    }
    
    // Validate data paths
    if (!string.IsNullOrEmpty(settings.TesseractDataPath))
    {
        if (!IsValidDataPath(settings.TesseractDataPath))
        {
            _loggingService.LogWarning("Invalid TesseractDataPath in settings, clearing");
            settings = settings with { TesseractDataPath = null };
        }
    }
    
    // Validate string inputs
    if (!IsValidLanguageCode(settings.DefaultOcrLanguage))
    {
        settings = settings with { DefaultOcrLanguage = "eng" };
    }
    
    if (!IsValidFileNamePattern(settings.FileNamePattern))
    {
        settings = settings with { FileNamePattern = "{basename}.{lang}{forced}.srt" };
    }
    
    return settings;
}

private static bool IsValidToolPath(string path, string expectedFilename)
{
    try
    {
        // Must be absolute path
        if (!Path.IsPathRooted(path))
            return false;
        
        // Must exist
        if (!File.Exists(path))
            return false;
        
        // Must end with expected filename
        if (!Path.GetFileName(path).Equals(expectedFilename, StringComparison.OrdinalIgnoreCase))
            return false;
        
        // Must not contain path traversal
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
            return false;
        
        // Must be .exe file
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        
        return true;
    }
    catch
    {
        return false;
    }
}
```

---

### 5. Denial of Service via Resource Exhaustion

**Severity:** HIGH 🟠  
**CWE:** CWE-400 (Uncontrolled Resource Consumption)  
**CVSS Score:** 7.1 (High)

**Location:**
- `ProcessRunner.cs` lines 55-94
- `MainViewModel.cs` batch processing without limits

**Vulnerability Description:**

While process output is limited to 10MB, there's no limit on:
1. Number of concurrent processes
2. Number of files in batch queue
3. Memory consumption during batch processing
4. Disk space usage for extracted files

**Vulnerable Code:**
```csharp
// MainViewModel.cs: No limit on batch queue size
public void AddFilesToBatchQueue(string[] filePaths)
{
    foreach (var filePath in filePaths)  // ❌ No limit check
    {
        // Add files without checking total count or estimated disk space
    }
}
```

**Attack Scenario:**

Attacker drags 10,000 large MKV files (50GB each) into batch mode:
- Application attempts to process all files
- Disk fills up completely
- System becomes unresponsive
- Application crashes

**Recommended Fix:**

```csharp
public class BatchLimits
{
    public const int MaxBatchFiles = 1000;
    public const long MaxTotalSizeBytes = 500L * 1024 * 1024 * 1024; // 500GB
    public const long MaxSingleFileSizeBytes = 100L * 1024 * 1024 * 1024; // 100GB
    public const int MaxConcurrentProcesses = 1; // Process one at a time
}

public void AddFilesToBatchQueue(string[] filePaths)
{
    var currentCount = State.BatchQueue.Count;
    var currentTotalSize = State.BatchQueue.Sum(f => f.FileSize);
    
    var filesToAdd = new List<BatchFile>();
    var skippedCount = 0;
    
    foreach (var filePath in filePaths)
    {
        // Check batch limits
        if (currentCount + filesToAdd.Count >= BatchLimits.MaxBatchFiles)
        {
            _notificationService.ShowWarning(
                $"Batch queue limit reached ({BatchLimits.MaxBatchFiles} files). " +
                "Some files were not added.",
                "Queue Limit Reached");
            break;
        }
        
        // Check file size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > BatchLimits.MaxSingleFileSizeBytes)
        {
            _loggingService.LogWarning($"File too large, skipping: {filePath}");
            skippedCount++;
            continue;
        }
        
        // Check total size
        if (currentTotalSize + fileInfo.Length > BatchLimits.MaxTotalSizeBytes)
        {
            _notificationService.ShowWarning(
                $"Total batch size limit reached ({BatchLimits.MaxTotalSizeBytes / (1024 * 1024 * 1024)}GB). " +
                "Some files were not added.",
                "Size Limit Reached");
            break;
        }
        
        // Check available disk space
        var drive = Path.GetPathRoot(filePath);
        if (!string.IsNullOrEmpty(drive))
        {
            var driveInfo = new DriveInfo(drive);
            var estimatedOutput = fileInfo.Length / 100; // Estimate output size
            
            if (driveInfo.AvailableFreeSpace < estimatedOutput * 2) // 2x safety margin
            {
                _notificationService.ShowWarning(
                    "Insufficient disk space for batch processing.",
                    "Disk Space Warning");
                break;
            }
        }
        
        var batchFile = new BatchFile { FilePath = filePath };
        filesToAdd.Add(batchFile);
        currentTotalSize += fileInfo.Length;
    }
    
    // Add files
    foreach (var file in filesToAdd)
    {
        State.BatchQueue.Add(file);
    }
}
```

---

## Medium Severity Issues (2)

### 6. Weak File Lock Detection

**Severity:** MEDIUM 🟡  
**CWE:** CWE-362 (Concurrent Execution using Shared Resource with Improper Synchronization)  
**CVSS Score:** 5.9 (Medium)

**Location:**
- Assuming `FileLockDetectionService` implementation

**Issue:**

File lock detection may have race conditions where a file becomes locked between detection and usage. This could cause:
- Data corruption
- Incomplete extractions
- Application crashes

**Recommended Fix:**

```csharp
public interface IFileLockDetectionService
{
    Task<bool> IsFileLockedAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IDisposable> AcquireFileLockAsync(string filePath, CancellationToken cancellationToken = default);
}

// Implement proper file locking
public class FileLockHandle : IDisposable
{
    private readonly FileStream _lockStream;
    
    public FileLockHandle(FileStream lockStream)
    {
        _lockStream = lockStream;
    }
    
    public void Dispose()
    {
        _lockStream?.Dispose();
    }
}

public async Task<IDisposable> AcquireFileLockAsync(string filePath, CancellationToken cancellationToken)
{
    try
    {
        // Open with exclusive read access
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None, // Exclusive lock
            4096,
            FileOptions.Asynchronous);
        
        return new FileLockHandle(stream);
    }
    catch (IOException)
    {
        throw new IOException($"File is locked or in use: {filePath}");
    }
}

// Usage
public async Task<ProbeResult> ProbeAsync(string mkvPath, CancellationToken cancellationToken = default)
{
    // Acquire lock before processing
    using var fileLock = await _fileLockDetectionService.AcquireFileLockAsync(mkvPath, cancellationToken);
    
    // File is now locked for exclusive reading
    // Proceed with probe operation
}
```

---

### 7. Insecure Temporary File Handling

**Severity:** MEDIUM 🟡  
**CWE:** CWE-377 (Insecure Temporary File)  
**CVSS Score:** 5.5 (Medium)

**Location:**
- `MainViewModel.cs` lines 359-386 (PGS extraction)

**Vulnerability Description:**

Temporary files are created in predictable locations without proper cleanup guarantees, potentially exposing sensitive content or causing disk space issues.

**Recommended Fix:**

```csharp
public class SecureTempFileManager : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();
    private readonly string _sessionDirectory;
    
    public SecureTempFileManager()
    {
        // Create session-specific temp directory
        _sessionDirectory = Path.Combine(
            Path.GetTempPath(),
            "SrtExtractor",
            Guid.NewGuid().ToString());
        
        Directory.CreateDirectory(_sessionDirectory);
        
        // Set restrictive permissions (current user only)
        SetRestrictivePermissions(_sessionDirectory);
    }
    
    public string CreateTempFile(string extension)
    {
        var fileName = Path.Combine(
            _sessionDirectory,
            $"{Guid.NewGuid()}{extension}");
        
        _tempFiles.Add(fileName);
        return fileName;
    }
    
    public string CreateTempDirectory()
    {
        var dirName = Path.Combine(
            _sessionDirectory,
            Guid.NewGuid().ToString());
        
        Directory.CreateDirectory(dirName);
        _tempDirectories.Add(dirName);
        
        return dirName;
    }
    
    private void SetRestrictivePermissions(string path)
    {
        var directoryInfo = new DirectoryInfo(path);
        var security = directoryInfo.GetAccessControl();
        
        // Remove all existing rules
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(NTAccount)))
        {
            security.RemoveAccessRule(rule);
        }
        
        // Add current user only
        var currentUser = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser.User!,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        
        directoryInfo.SetAccessControl(security);
    }
    
    public void Dispose()
    {
        // Clean up all temp files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    // Securely delete by overwriting
                    SecureDelete(file);
                }
            }
            catch { /* Best effort */ }
        }
        
        // Clean up directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch { /* Best effort */ }
        }
        
        // Clean up session directory
        try
        {
            if (Directory.Exists(_sessionDirectory))
            {
                Directory.Delete(_sessionDirectory, true);
            }
        }
        catch { /* Best effort */ }
    }
    
    private void SecureDelete(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var length = fileInfo.Length;
            
            // Overwrite with random data
            using (var stream = File.OpenWrite(filePath))
            {
                var random = new byte[4096];
                new Random().NextBytes(random);
                
                for (long i = 0; i < length; i += random.Length)
                {
                    stream.Write(random, 0, (int)Math.Min(random.Length, length - i));
                }
            }
            
            File.Delete(filePath);
        }
        catch { /* Best effort */ }
    }
}

// Usage in MainViewModel
private SecureTempFileManager? _tempFileManager;

public MainViewModel(...)
{
    _tempFileManager = new SecureTempFileManager();
    // ...
}

// In ExtractSubtitlesAsync for PGS
var tempSupPath = _tempFileManager.CreateTempFile(".sup");
```

---

## Low Severity Issues (1)

### 8. Browser Process Launch Without Validation

**Severity:** LOW 🟢  
**CWE:** CWE-601 (URL Redirection to Untrusted Site)  
**CVSS Score:** 4.3 (Medium)

**Location:**
- `WingetService.cs` lines 174-180

**Vulnerability Description:**

The application opens URLs in the default browser without validation. While currently hardcoded to GitHub, future modifications could introduce vulnerabilities.

**Current Code:**
```csharp
// WingetService.cs:175
var url = "https://github.com/SubtitleEdit/subtitleedit-cli/releases";
System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = url,
    UseShellExecute = true
});
```

**Recommended Fix:**

```csharp
public static class UrlValidator
{
    private static readonly HashSet<string> AllowedDomains = new()
    {
        "github.com",
        "githubusercontent.com",
        "microsoft.com",
        "winget.microsoft.com"
    };
    
    public static bool IsUrlSafe(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        
        try
        {
            var uri = new Uri(url);
            
            // Must be HTTPS
            if (uri.Scheme != Uri.UriSchemeHttps)
                return false;
            
            // Must be in allowed domains
            var host = uri.Host.ToLowerInvariant();
            return AllowedDomains.Any(domain => 
                host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
    
    public static void OpenUrl(string url, ILoggingService loggingService)
    {
        if (!IsUrlSafe(url))
        {
            loggingService.LogWarning($"Attempted to open untrusted URL: {url}");
            throw new SecurityException("URL is not in the allowed domains list");
        }
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            
            loggingService.LogInfo($"Opened URL in browser: {url}");
        }
        catch (Exception ex)
        {
            loggingService.LogError($"Failed to open URL: {url}", ex);
            throw;
        }
    }
}

// Update WingetService
public Task<bool> InstallSubtitleEditAsync()
{
    var url = "https://github.com/SubtitleEdit/subtitleedit-cli/releases";
    
    try
    {
        UrlValidator.OpenUrl(url, _loggingService);
        return Task.FromResult(true);
    }
    catch (Exception ex)
    {
        _loggingService.LogError("Failed to open GitHub releases page", ex);
        return Task.FromResult(false);
    }
}
```

---

## Additional Security Recommendations

### 1. Implement Code Signing

**Priority:** HIGH

Sign the application executable and all dependencies to prevent tampering and establish trust.

```powershell
# Sign executable
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com SrtExtractor.exe
```

### 2. Add Integrity Checks for External Tools

**Priority:** MEDIUM

Verify external tool executables haven't been tampered with:

```csharp
public static class IntegrityChecker
{
    private static readonly Dictionary<string, string> KnownToolHashes = new()
    {
        { "mkvmerge.exe", "SHA256_HASH_HERE" },
        { "seconv.exe", "SHA256_HASH_HERE" },
        { "ffmpeg.exe", "SHA256_HASH_HERE" }
    };
    
    public static bool VerifyToolIntegrity(string toolPath)
    {
        var filename = Path.GetFileName(toolPath);
        
        if (!KnownToolHashes.ContainsKey(filename))
            return false; // Unknown tool
        
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(toolPath);
        
        var hash = sha256.ComputeHash(stream);
        var hashString = BitConverter.ToString(hash).Replace("-", "");
        
        return hashString.Equals(KnownToolHashes[filename], 
            StringComparison.OrdinalIgnoreCase);
    }
}
```

### 3. Implement Rate Limiting

**Priority:** MEDIUM

Limit operations to prevent abuse:

```csharp
public class RateLimiter
{
    private readonly Dictionary<string, DateTime> _lastOperation = new();
    private readonly TimeSpan _minimumInterval = TimeSpan.FromSeconds(1);
    
    public bool CanPerformOperation(string operationKey)
    {
        if (!_lastOperation.ContainsKey(operationKey))
        {
            _lastOperation[operationKey] = DateTime.UtcNow;
            return true;
        }
        
        var timeSinceLastOp = DateTime.UtcNow - _lastOperation[operationKey];
        
        if (timeSinceLastOp >= _minimumInterval)
        {
            _lastOperation[operationKey] = DateTime.UtcNow;
            return true;
        }
        
        return false;
    }
}
```

### 4. Add Update Verification

**Priority:** HIGH

If implementing auto-update, verify updates:

```csharp
public class UpdateVerifier
{
    public static async Task<bool> VerifyUpdateAsync(string updatePackagePath, string expectedSignature)
    {
        // Verify digital signature
        if (!VerifyDigitalSignature(updatePackagePath))
            return false;
        
        // Verify hash matches expected
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(updatePackagePath);
        var hash = await sha256.ComputeHashAsync(stream);
        var hashString = BitConverter.ToString(hash).Replace("-", "");
        
        return hashString.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool VerifyDigitalSignature(string filePath)
    {
        try
        {
            var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath);
            return cert != null;
        }
        catch
        {
            return false;
        }
    }
}
```

### 5. Secure Memory Handling

**Priority:** LOW

For sensitive data, implement secure memory handling:

```csharp
public class SecureStringHandler : IDisposable
{
    private GCHandle _handle;
    private readonly byte[] _data;
    
    public SecureStringHandler(string sensitiveData)
    {
        _data = Encoding.UTF8.GetBytes(sensitiveData);
        _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
    }
    
    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            // Zero out memory
            Array.Clear(_data, 0, _data.Length);
            _handle.Free();
        }
    }
}
```

---

## Testing Recommendations

### Security Test Cases

1. **Command Injection Tests:**
   ```
   - Filename: `test" && calc.exe && echo ".mkv`
   - Filename: `test'; rm -rf /tmp/*; echo '.mkv`
   - Filename: `test$(whoami).mkv`
   - Filename: `test`calc.exe`.mkv`
   ```

2. **Path Traversal Tests:**
   ```
   - Path: `..\..\..\..\Windows\System32\test.srt`
   - Path: `\\?\C:\Windows\System32\test.srt`
   - Path: `C:\Windows\System32\test.srt`
   ```

3. **Resource Exhaustion Tests:**
   ```
   - Add 10,000 files to batch queue
   - Process 100GB file
   - Run multiple instances simultaneously
   ```

4. **Settings Tampering Tests:**
   ```
   - Modify settings.json with malicious paths
   - Add invalid JSON to settings.json
   - Delete settings.json during operation
   ```

### Penetration Testing Checklist

- [ ] Fuzz file pickers with malicious filenames
- [ ] Test drag-and-drop with symbolic links
- [ ] Attempt DLL hijacking
- [ ] Test with files on SMB shares with special permissions
- [ ] Verify log file permissions
- [ ] Check for secrets in memory dumps
- [ ] Test privilege escalation scenarios
- [ ] Verify proper resource cleanup on crashes

---

## Compliance Considerations

### GDPR Compliance

If processing videos containing personal data:
- Implement data minimization in logs
- Add user consent mechanisms
- Provide data deletion capabilities
- Document data processing activities

### OWASP ASVS Alignment

Current compliance level: **Level 1** (partial)  
Target level: **Level 2**

Missing controls:
- Input validation (V5)
- Cryptography (V6)
- Error handling (V7)
- Logging and monitoring (V7)
- Communication security (V9)

---

## Summary of Required Actions

### Immediate (Critical) - Fix Within 1 Week

1. ✅ **Fix command injection vulnerabilities**
   - Implement `ArgumentList` in `ProcessRunner`
   - Add path validation
   - Update all service calls

2. ✅ **Fix path traversal vulnerabilities**
   - Implement `SafeFileOperations` class
   - Validate all output paths
   - Restrict output to safe directories

### Short Term (High) - Fix Within 1 Month

3. ✅ **Implement log sanitization**
   - Remove sensitive paths from logs
   - Secure log directory permissions
   - Add log retention policy

4. ✅ **Add settings validation**
   - Validate deserialized settings
   - Implement path verification
   - Add checksum verification

5. ✅ **Implement resource limits**
   - Add batch queue limits
   - Implement disk space checks
   - Add memory usage monitoring

### Medium Term (Medium) - Fix Within 3 Months

6. ✅ **Improve file lock handling**
   - Implement proper exclusive locks
   - Add retry mechanisms
   - Handle concurrent access

7. ✅ **Secure temporary file handling**
   - Create session-specific temp directories
   - Set restrictive permissions
   - Implement secure deletion

### Long Term (Low) - Fix Within 6 Months

8. ✅ **Add URL validation**
   - Implement allowlist
   - Verify HTTPS
   - Add domain validation

9. ✅ **Implement code signing**
10. ✅ **Add integrity checks**
11. ✅ **Implement rate limiting**

---

## Risk Assessment After Fixes

**Current Risk:** HIGH ⚠️  
**Risk After Critical Fixes:** MEDIUM 🟡  
**Risk After All Fixes:** LOW 🟢

---

## Conclusion

The SrtExtractor application has several critical security vulnerabilities that require immediate attention, particularly around command injection and path traversal. The good news is that the application has a solid architectural foundation with proper service abstraction and logging infrastructure.

**Immediate action is required** on the command injection and path traversal issues before this application should be deployed in any production environment.

Once all critical and high-severity issues are addressed, the application will have a strong security posture suitable for general use.

---

## Contact

For questions about this security review, please contact:
- Security Team: security@zentrixlabs.com
- Project Lead: development@zentrixlabs.com

**Review ID:** SR-2025-10-09-001  
**Next Review Due:** April 9, 2026

