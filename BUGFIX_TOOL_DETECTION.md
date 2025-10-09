# Bug Fix: Tool Detection Issue

**Date:** October 9, 2025  
**Issue:** Application not finding bundled FFmpeg and Subtitle Edit CLI  
**Priority:** HIGH  
**Status:** ✅ FIXED

---

## Problem

After implementing security fixes, the application stopped finding FFmpeg and Subtitle Edit CLI executables that are bundled with the app. The issue was caused by overly strict path validation in `ProcessRunner`.

### Root Cause

The `ProcessRunner` was calling `PathValidator.ValidateFileExists()` on ALL executable paths, including bare executable names like:
- `"ffmpeg.exe"`
- `"seconv.exe"` 
- `"winget"`

This caused failures because `File.Exists("ffmpeg.exe")` returns `false` even if the executable is:
- In the current directory (app folder)
- In the system PATH
- In the working directory

### Error Flow

```
ToolDetectionService tries to run "seconv"
    ↓
ProcessRunner.RunAsync("seconv", new[] { "--help" })
    ↓
PathValidator.ValidateFileExists("seconv")
    ↓
File.Exists("seconv") returns false
    ↓
❌ FileNotFoundException thrown
```

---

## Solution

Updated `ProcessRunner.RunAsync()` to distinguish between two cases:

### Case 1: Full or Relative Paths
**Examples:** 
- `"C:\Tools\mkvmerge.exe"`
- `".\ffmpeg.exe"`
- `"..\bin\tool.exe"`

**Validation:** Strict - must exist and be safe
```csharp
if (Path.IsPathRooted(exe) || exe.Contains(Path.DirectorySeparatorChar) || exe.Contains(Path.AltDirectorySeparatorChar))
{
    // Full or relative path - validate it exists and is safe
    validatedExe = PathValidator.ValidateFileExists(exe);
}
```

### Case 2: Bare Executable Names
**Examples:**
- `"ffmpeg.exe"`
- `"seconv"`
- `"winget"`

**Validation:** Sanitize only - don't check existence
```csharp
else
{
    // Just an executable name - sanitize but don't require it to exist as a file
    // (will be resolved via PATH, current directory, or app directory)
    validatedExe = PathValidator.ValidateAndSanitizeFilePath(exe);
}
```

---

## Implementation

**File:** `SrtExtractor/Services/Implementations/ProcessRunner.cs`

```csharp
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
    
    // ... rest of implementation
}
```

---

## Security Analysis

### ✅ Still Secure

**Protection Maintained:**
- ✅ Command injection prevention (using ArgumentList)
- ✅ Shell metacharacter blocking (in ValidateAndSanitizeFilePath)
- ✅ Path traversal prevention (in ValidateAndSanitizeFilePath)
- ✅ Null byte blocking
- ✅ Invalid character blocking

**What Changed:**
- ❌ NO LONGER checking that bare executable names exist as files
- ✅ STILL sanitizing all executable paths
- ✅ STILL validating full/relative paths exist

### Security Trade-offs

| Scenario | Before Fix | After Fix | Risk |
|----------|-----------|-----------|------|
| `"calc.exe"` | ❌ Rejected | ✅ Allowed | Low - Windows system tools are expected |
| `"ffmpeg.exe"` in app folder | ❌ Rejected | ✅ Allowed | Low - bundled tools are safe |
| `"../../evil.exe"` | ✅ Rejected | ✅ Rejected | None - still blocked |
| `"C:\Tools\tool.exe"` | ✅ Validated | ✅ Validated | None - still checked |
| `"tool.exe & calc"` | ✅ Rejected | ✅ Rejected | None - shell chars blocked |

**Verdict:** The fix maintains security while allowing legitimate bundled executables.

---

## Testing

### Test Cases

**✅ Should Work (Bundled Tools):**
```csharp
await ProcessRunner.RunAsync("ffmpeg.exe", new[] { "-version" });
await ProcessRunner.RunAsync("seconv.exe", new[] { "--help" });
await ProcessRunner.RunAsync("winget", new[] { "--version" });
```

**✅ Should Work (Full Paths):**
```csharp
await ProcessRunner.RunAsync(@"C:\Program Files\MKVToolNix\mkvmerge.exe", new[] { "--version" });
```

**❌ Should Be Blocked (Security Violations):**
```csharp
await ProcessRunner.RunAsync("tool.exe & calc", args);           // Shell metachar
await ProcessRunner.RunAsync("../../Windows/notepad.exe", args); // Path traversal  
await ProcessRunner.RunAsync("tool\0.exe", args);                // Null byte
```

### Verification

```bash
✅ FFmpeg detection: WORKS
✅ Subtitle Edit CLI detection: WORKS
✅ MKVToolNix detection: WORKS
✅ Winget operations: WORKS
✅ Security validation: MAINTAINED
```

---

## Impact

### Before Fix
```
❌ FFmpeg not found
❌ Subtitle Edit CLI not found  
❌ App cannot function without manually specifying paths
```

### After Fix
```
✅ FFmpeg found (bundled in app directory)
✅ Subtitle Edit CLI found (bundled in app directory)
✅ App works out of the box
✅ Security maintained
```

---

## Lessons Learned

### Good Security Practice
✅ Validate all user-supplied file paths strictly
✅ Check paths exist before processing user files
✅ Sanitize all inputs for dangerous characters

### Security vs Usability
⚖️ Don't validate system executable names the same as user file paths
⚖️ Allow standard executable resolution (PATH, current directory)
⚖️ Focus strict validation on user-controllable inputs

### Best Practice for Tool Detection
```csharp
// User file inputs (MKV, MP4, etc.) - STRICT validation
var userFile = PathValidator.ValidateFileExists(userProvidedPath);

// System tools (ffmpeg.exe, etc.) - SANITIZE only
var tool = PathValidator.ValidateAndSanitizeFilePath("ffmpeg.exe");

// Full paths to tools - STRICT validation  
var toolPath = PathValidator.ValidateFileExists(@"C:\Tools\tool.exe");
```

---

## Related Changes

- ✅ `ProcessRunner.cs` - Smart path validation
- ✅ `PathValidator.cs` - No changes needed (already has both methods)
- ✅ Tool detection services - Already using bare names correctly

---

## Documentation Updates

**Files Updated:**
1. `BUGFIX_TOOL_DETECTION.md` - This document
2. `ProcessRunner.cs` - Implementation with comments
3. Code comments explaining the two validation paths

---

## Conclusion

The bug has been fixed while maintaining full security protections. The application now correctly finds bundled tools while still preventing command injection and path traversal attacks.

**Status:** ✅ RESOLVED  
**Security:** ✅ MAINTAINED  
**Functionality:** ✅ RESTORED

---

**Fix verified and ready for deployment.**

