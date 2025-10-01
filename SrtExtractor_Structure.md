# SrtExtractor - Project Structure & Standards

## 🏗️ Project Architecture

### **MVVM Pattern Implementation**
- **Models**: Data structures and business entities
- **Views**: XAML UI components (MainWindow.xaml)
- **ViewModels**: Business logic and UI state management
- **Services**: External dependencies and business operations
- **State**: Observable state objects for data binding

### **Service Layer Architecture**
All external dependencies and business operations are abstracted through service interfaces:

```
Services/
├── Interfaces/
│   ├── IProcessRunner.cs          # External process execution
│   ├── IToolDetectionService.cs   # Tool discovery and validation
│   ├── IWingetService.cs          # Package management
│   ├── IMkvToolService.cs         # MKVToolNix operations
│   ├── ISubtitleOcrService.cs     # Subtitle Edit OCR
│   ├── ISettingsService.cs        # Configuration management
│   └── ILoggingService.cs         # Centralized logging
├── Implementations/
│   ├── ProcessRunner.cs
│   ├── ToolDetectionService.cs
│   ├── WingetService.cs
│   ├── MkvToolService.cs
│   ├── SubtitleOcrService.cs
│   ├── SettingsService.cs
│   └── LoggingService.cs
```

## 📁 Folder Structure

```
SrtExtractor/
├── Models/                        # Data models and entities
│   ├── SubtitleTrack.cs
│   ├── ProbeResult.cs
│   ├── ToolStatus.cs
│   ├── AppSettings.cs
│   └── ToolErrorType.cs
├── ViewModels/                    # MVVM ViewModels
│   └── MainViewModel.cs
├── Views/                         # XAML Views
│   └── MainWindow.xaml
├── State/                         # Observable state objects
│   └── ExtractionState.cs
├── Services/                      # Service layer
│   ├── Interfaces/
│   └── Implementations/
├── Converters/                    # Value converters for UI
│   ├── BoolToStatusConverter.cs
│   └── InverseBoolToVisibilityConverter.cs
├── Extensions/                    # Extension methods
│   └── StringExtensions.cs
├── Utils/                         # Utility classes
│   └── FileUtils.cs
├── App.xaml
├── App.xaml.cs
└── SrtExtractor.csproj
```

## 🔧 Service Interfaces

### **IProcessRunner**
```csharp
public interface IProcessRunner
{
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string exe, 
        string args, 
        CancellationToken ct = default);
}
```

### **IToolDetectionService**
```csharp
public interface IToolDetectionService
{
    Task<ToolStatus> CheckMkvToolNixAsync();
    Task<ToolStatus> CheckSubtitleEditAsync();
    Task<string?> FindToolPathAsync(string toolName, string[] commonPaths);
    Task<bool> ValidateToolAsync(string toolPath);
}
```

### **IWingetService**
```csharp
public interface IWingetService
{
    Task<bool> IsWingetAvailableAsync();
    Task<bool> InstallPackageAsync(string packageId);
    Task<string?> GetInstalledVersionAsync(string packageId);
    Task<bool> IsPackageInstalledAsync(string packageId);
}
```

### **IMkvToolService**
```csharp
public interface IMkvToolService
{
    Task<ProbeResult> ProbeAsync(string mkvPath);
    Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt);
    Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup);
}
```

### **ISubtitleOcrService**
```csharp
public interface ISubtitleOcrService
{
    Task OcrSupToSrtAsync(
        string supPath, 
        string outSrt, 
        string language, 
        bool fixCommonErrors = true, 
        bool removeHi = true);
}
```

### **ISettingsService**
```csharp
public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<string> GetAppDataPathAsync();
}
```

### **ILoggingService**
```csharp
public interface ILoggingService
{
    void LogInfo(string message, [CallerMemberName] string memberName = "");
    void LogWarning(string message, [CallerMemberName] string memberName = "");
    void LogError(string message, Exception? exception = null, [CallerMemberName] string memberName = "");
    void LogToolDetection(string toolName, ToolStatus status);
    void LogInstallation(string toolName, bool success, string? error = null);
    void LogExtraction(string operation, bool success, string? error = null);
}
```

## 📊 Data Models

### **Core Models**
```csharp
public record SubtitleTrack(
    int Id,
    string Codec,
    string Language,
    bool Forced,
    string? Name
);

public record ProbeResult(IReadOnlyList<SubtitleTrack> Tracks);
```

### **Tool Management Models**
```csharp
public record ToolStatus(
    bool IsInstalled,
    string? Path,
    string? Version,
    string? ErrorMessage
);

public enum ToolErrorType
{
    NotFound,
    PathInvalid,
    VersionIncompatible,
    ExecutionFailed,
    WingetNotAvailable
}
```

### **Settings Model**
```csharp
public record AppSettings(
    string? MkvMergePath,
    string? MkvExtractPath,
    string? SubtitleEditPath,
    bool AutoDetectTools,
    DateTime? LastToolCheck,
    bool PreferForced,
    string DefaultOcrLanguage,
    string FileNamePattern
);
```

## 🎯 MVVM Implementation

### **ExtractionState (Observable State)**
```csharp
public class ExtractionState : ObservableObject
{
    // File Management
    [ObservableProperty]
    private string? _mkvPath;
    
    [ObservableProperty]
    private ObservableCollection<SubtitleTrack> _tracks = new();
    
    [ObservableProperty]
    private SubtitleTrack? _selectedTrack;
    
    // Tool Status
    [ObservableProperty]
    private ToolStatus _mkvToolNixStatus = new(false, null, null, null);
    
    [ObservableProperty]
    private ToolStatus _subtitleEditStatus = new(false, null, null, null);
    
    // Computed Properties
    public bool AreToolsAvailable => MkvToolNixStatus.IsInstalled && SubtitleEditStatus.IsInstalled;
    public bool CanProbe => !string.IsNullOrEmpty(MkvPath) && AreToolsAvailable;
    public bool CanExtract => SelectedTrack != null && AreToolsAvailable;
    
    // Settings
    [ObservableProperty]
    private bool _preferForced = true;
    
    [ObservableProperty]
    private string _ocrLanguage = "eng";
    
    // UI State
    [ObservableProperty]
    private bool _isBusy;
    
    [ObservableProperty]
    private string _logText = string.Empty;
}
```

### **MainViewModel**
```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;
    private readonly IToolDetectionService _toolDetectionService;
    private readonly IWingetService _wingetService;
    private readonly IMkvToolService _mkvToolService;
    private readonly ISubtitleOcrService _ocrService;
    private readonly ISettingsService _settingsService;
    
    [ObservableProperty]
    private ExtractionState _state = new();
    
    // Commands
    public IRelayCommand PickMkvCommand { get; }
    public IRelayCommand ProbeCommand { get; }
    public IRelayCommand ExtractCommand { get; }
    public IRelayCommand InstallMkvToolNixCommand { get; }
    public IRelayCommand InstallSubtitleEditCommand { get; }
    public IRelayCommand BrowseMkvToolNixCommand { get; }
    public IRelayCommand BrowseSubtitleEditCommand { get; }
}
```

## 📝 Logging Standards

### **Centralized Logging Service**
- **Location**: `C:\ProgramData\ZentrixLabs\SrtExtractor\Logs\`
- **Format**: `srt_YYYYMMDD.txt` (rolling daily logs)
- **Levels**: Info, Warning, Error
- **Categories**: Tool Detection, Installation, Extraction, General

### **Logging Rules**
1. **NO PRINT STATEMENTS** - Use `ILoggingService` exclusively
2. **Structured Logging** - Include context and caller information
3. **Error Context** - Always include exception details for errors
4. **Performance** - Log timing for long operations
5. **User Actions** - Log significant user interactions

### **Logging Examples**
```csharp
// ✅ Correct
_loggingService.LogInfo($"Starting tool detection for {toolName}");
_loggingService.LogError("Failed to extract subtitles", exception);
_loggingService.LogToolDetection("MKVToolNix", toolStatus);

// ❌ Incorrect
Console.WriteLine("Starting tool detection");
Debug.WriteLine("Error occurred");
```

## 🚫 Error Handling Standards

### **Centralized Error Handling**
- **Service Layer**: All services handle their own errors and log them
- **ViewModel Layer**: Catch service errors and update UI state
- **View Layer**: Display user-friendly error messages

### **Error Handling Pattern**
```csharp
public async Task<bool> SomeOperationAsync()
{
    try
    {
        _loggingService.LogInfo("Starting operation");
        var result = await _service.DoSomethingAsync();
        _loggingService.LogInfo("Operation completed successfully");
        return true;
    }
    catch (Exception ex)
    {
        _loggingService.LogError("Operation failed", ex);
        State.IsBusy = false;
        // Update UI with user-friendly message
        return false;
    }
}
```

## 🎨 UI Standards

### **XAML Structure**
- **Data Binding**: Use `{Binding}` with proper property paths
- **Commands**: Use `{Binding CommandName}` for button actions
- **Converters**: Use value converters for UI transformations
- **Resources**: Define styles and templates in App.xaml

### **UI Binding Examples**
```xml
<Button Content="Probe Tracks" 
        Command="{Binding ProbeCommand}"
        IsEnabled="{Binding State.CanProbe}"/>

<TextBlock Text="{Binding State.MkvToolNixStatus.IsInstalled, 
                  Converter={StaticResource BoolToStatusConverter}}"/>

<DataGrid ItemsSource="{Binding State.Tracks}"
          SelectedItem="{Binding State.SelectedTrack}"/>
```

## 🔧 Dependency Injection

### **Service Registration**
```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Register services
        var services = new ServiceCollection();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IToolDetectionService, ToolDetectionService>();
        services.AddSingleton<IWingetService, WingetService>();
        services.AddSingleton<IMkvToolService, MkvToolService>();
        services.AddSingleton<ISubtitleOcrService, SubtitleOcrService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Set up MainWindow with DI
        var mainWindow = new MainWindow();
        mainWindow.DataContext = serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
        
        base.OnStartup(e);
    }
}
```

## 📋 Code Quality Standards

### **Naming Conventions**
- **Classes**: PascalCase (e.g., `MainViewModel`)
- **Methods**: PascalCase (e.g., `ExtractSubtitlesAsync`)
- **Properties**: PascalCase (e.g., `CanProbe`)
- **Fields**: camelCase with underscore (e.g., `_loggingService`)
- **Constants**: PascalCase (e.g., `DefaultOcrLanguage`)

### **Async/Await Patterns**
- **All I/O operations**: Use async/await
- **Service methods**: Return Task or Task<T>
- **UI operations**: Use ConfigureAwait(false) for service calls
- **Cancellation**: Support CancellationToken where appropriate

### **Exception Handling**
- **Service Layer**: Catch, log, and rethrow or return error status
- **ViewModel Layer**: Catch service exceptions and update UI
- **Never**: Swallow exceptions silently
- **Always**: Provide meaningful error messages to users

## 🧪 Testing Strategy

### **Unit Testing**
- **Services**: Mock dependencies and test business logic
- **ViewModels**: Test command execution and state updates
- **Models**: Test data validation and transformations

### **Integration Testing**
- **Tool Detection**: Test with actual tool installations
- **File Operations**: Test with sample MKV files
- **Error Scenarios**: Test missing tools and invalid files

## 📚 Documentation Standards

### **Code Documentation**
- **Public APIs**: XML documentation comments
- **Complex Logic**: Inline comments explaining business rules
- **TODO Items**: Use TODO comments for future improvements
- **Deprecated Code**: Mark with Obsolete attribute

### **README Updates**
- **Setup Instructions**: Include tool installation steps
- **Usage Examples**: Show common workflows
- **Troubleshooting**: Document common issues and solutions
- **Architecture**: Explain design decisions and patterns

---

This structure ensures maintainable, testable, and scalable code while following WPF/MVVM best practices and centralized error handling standards.
