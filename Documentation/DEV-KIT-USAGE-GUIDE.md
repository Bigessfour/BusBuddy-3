# 🚌 BusBuddy C# Dev Kit Usage Guide

## 🎯 **Key Features & Shortcuts**

### **1. Solution Explorer**
- **Access**: `Ctrl+Shift+P` → "Solution Explorer: Focus"
- **Functions**:
  - Browse project structure
  - Add new files/folders
  - Build individual projects
  - Manage project references

### **2. Test Explorer**
- **Access**: `Ctrl+Shift+T` or Test icon in sidebar
- **Functions**:
  - Run all tests: `Ctrl+R, A`
  - Run specific test: Right-click → Run Test
  - Debug test: Right-click → Debug Test
  - View test results and coverage

### **3. IntelliSense & Code Navigation**
- **Go to Definition**: `F12`
- **Go to References**: `Shift+F12`
- **Quick Fix**: `Ctrl+.`
- **Rename Symbol**: `F2`
- **Format Document**: `Shift+Alt+F`

### **4. Debugging**
- **Start Debugging**: `F5`
- **Start Without Debugging**: `Ctrl+F5`
- **Toggle Breakpoint**: `F9`
- **Step Over**: `F10`
- **Step Into**: `F11`
- **Step Out**: `Shift+F11`

### **5. Build & Run**
- **Build Solution**: `Ctrl+Shift+B`
- **Build Project**: Right-click project → Build
- **Clean Solution**: Command Palette → "Clean"
- **Restore NuGet**: Command Palette → "Restore"

## 🧪 **Testing with Dev Kit**

### **Unit Test Creation**
```csharp
[Test]
public void TestName()
{
    // Arrange
    var service = new MyService();

    // Act
    var result = service.DoSomething();

    // Assert
    Assert.That(result, Is.Not.Null);
}
```

### **Test Categories**
- **Unit Tests**: Fast, isolated tests
- **Integration Tests**: Database and service tests
- **UI Tests**: WPF interaction tests

## 🔧 **Project Management**

### **Adding New Files**
1. Right-click project in Solution Explorer
2. Add → New File
3. Choose template (Class, Interface, etc.)

### **Managing Dependencies**
1. Right-click project → Add Project Reference
2. For NuGet: Right-click → Manage NuGet Packages
3. For project reference: Dependencies → Add Project Reference

## 🎨 **Code Generation**

### **Class Templates**
- `class`: Generate class template
- `interface`: Generate interface template
- `prop`: Generate property with get/set
- `ctor`: Generate constructor

### **MVVM Templates**
```csharp
// ViewModel with ObservableObject
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [RelayCommand]
    private void DoAction()
    {
        // Command implementation
    }
}
```

## 🚀 **BusBuddy-Specific Workflows**

### **1. Add New Business Entity**
1. Add model in `BusBuddy.Core/Models/`
2. Add service in `BusBuddy.Core/Services/`
3. Add ViewModel in `BusBuddy.WPF/ViewModels/`
4. Add View in `BusBuddy.WPF/Views/`
5. Add tests in `BusBuddy.Tests/`

### **2. Database Migration**
1. Modify entity in Core/Models
2. Run: `dotnet ef migrations add MigrationName`
3. Run: `dotnet ef database update`
4. Test with integration tests

### **3. Debug WPF Application**
1. Set breakpoints in ViewModels or Services
2. Press `F5` to start debugging
3. Use Debug Console for immediate evaluation
4. Use Call Stack for navigation

## ⚡ **Performance Tips**

### **IntelliSense Optimization**
- Use `using` statements properly
- Keep solution size reasonable
- Close unused projects when working on large solutions

### **Build Performance**
- Use incremental builds
- Exclude bin/obj from file watchers
- Use solution filters for large projects

## 🔍 **Troubleshooting**

### **Common Issues**
1. **IntelliSense not working**: Reload VS Code window
2. **Tests not discovered**: Clean and rebuild solution
3. **Debug not starting**: Check launch.json configuration
4. **References not resolving**: Restore NuGet packages

### **Dev Kit Logs**
- View → Output → Select "C# Dev Kit" or "C#"
- Check for error messages and warnings
- Look for loading status of projects

## 📚 **Resources**
- [C# Dev Kit Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [.NET Testing Guide](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [WPF Development Best Practices](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
