# Asset Management

This project includes the necessary branding assets directly in the repository for simplicity and to avoid external dependencies.

## 📁 Asset Structure

```
SrtExtractor/
├── ZLLogo.png          # ZentrixLabs logo for the WPF application
├── SrtExtractor.ico    # Application icon
assets/
├── ZLLogo.png          # Logo copy for reference
ZLLogo.png              # Logo copy in project root
```

## 🎯 Usage

### **WPF Application**
The logo is referenced directly in the project:
```csharp
// Logo is available as ZLLogo.png in the project directory
var logoPath = "ZLLogo.png";
```

### **Installer**
The installer uses the application icon and logo files directly from the project.

## 📝 Benefits

- ✅ **No External Dependencies**: Assets are included in the repository
- ✅ **Simple Build Process**: No asset syncing required
- ✅ **Open Source Friendly**: Anyone can build without setup
- ✅ **Version Control**: Asset changes are tracked with the code
- ✅ **Reliable Builds**: No network dependencies or authentication required

## 🔄 Updating Assets

When you need to update the ZentrixLabs logo or other assets:

1. **Get the latest assets** from the zentrixlabs-media repository
2. **Replace the files** in this project:
   - `SrtExtractor/ZLLogo.png`
   - `ZLLogo.png` (if used elsewhere)
   - `assets/ZLLogo.png` (if needed for reference)
3. **Commit the changes** along with your code changes

## 📚 Related Files

- `SrtExtractor/ZLLogo.png` - Main logo file for the application
- `SrtExtractor/SrtExtractor.ico` - Application icon
- `build.bat` - Simple build script (no asset syncing)
- `build-installer.ps1` - Installer build script

---

**Note**: This approach prioritizes simplicity and reliability over automatic asset updates. The zentrixlabs-media repository remains the central location for brand assets, but each project includes the assets it needs directly.