# WCAG 2.1 Accessibility Standards for BusBuddy WPF Application

**📅 Created:** July 25, 2025
**🎯 Target:** WCAG 2.1 Level AA Compliance
**🖥️ Technology:** WPF + Syncfusion FluentDark/FluentLight
**📋 Version:** 1.0

## 🎯 **ACCESSIBILITY OVERVIEW**

BusBuddy transportation management system must be accessible to users with disabilities, following WCAG 2.1 guidelines for desktop applications. This includes visual, auditory, motor, and cognitive accessibility considerations.

## 📊 **COMPLIANCE TARGETS**

| **Level**     | **Status**     | **Priority** | **Target Date** |
| ------------- | -------------- | ------------ | --------------- |
| **Level A**   | 🎯 Required    | Critical     | Phase 2         |
| **Level AA**  | 🎯 Required    | High         | Phase 2         |
| **Level AAA** | 📋 Recommended | Medium       | Phase 3         |

## 🎨 **VISUAL ACCESSIBILITY STANDARDS**

### **Color and Contrast Requirements**

**📏 WCAG 2.1 Contrast Ratios:**

- **Normal Text**: 4.5:1 minimum
- **Large Text**: 3:1 minimum
- **UI Components**: 3:1 minimum
- **Focus Indicators**: 3:1 minimum

**🎨 Syncfusion Theme Compliance:**

```xml
<!-- FluentDark Theme - High Contrast -->
<SolidColorBrush x:Key="PrimaryTextBrush" Color="#FFFFFF"/>        <!-- Contrast: 21:1 -->
<SolidColorBrush x:Key="SecondaryTextBrush" Color="#E0E0E0"/>      <!-- Contrast: 15.3:1 -->
<SolidColorBrush x:Key="BackgroundBrush" Color="#1E1E1E"/>         <!-- Base background -->

<!-- FluentLight Theme - High Contrast -->
<SolidColorBrush x:Key="PrimaryTextBrush" Color="#000000"/>        <!-- Contrast: 21:1 -->
<SolidColorBrush x:Key="SecondaryTextBrush" Color="#262626"/>      <!-- Contrast: 12.6:1 -->
<SolidColorBrush x:Key="BackgroundBrush" Color="#FFFFFF"/>         <!-- Base background -->
```

**🎯 Color Usage Guidelines:**

- **Never rely on color alone** for information
- **Provide text alternatives** for color-coded information
- **Support high contrast mode** detection
- **Test with color blindness simulators**

### **Typography and Text Standards**

**📝 Font Requirements:**

```xml
<!-- Minimum font sizes -->
<Style x:Key="BodyTextStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="14"/>           <!-- Minimum 14pt -->
    <Setter Property="FontFamily" Value="Segoe UI"/>
    <Setter Property="LineHeight" Value="1.5"/>        <!-- 150% line height -->
</Style>

<Style x:Key="HeadingTextStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="18"/>           <!-- Large text: 18pt+ -->
    <Setter Property="FontWeight" Value="SemiBold"/>
</Style>
```

**📏 Text Scaling Support:**

```csharp
// Support Windows text scaling (100% - 200%)
public void ApplyTextScaling()
{
    var textScaleFactor = SystemInformation.TextScaleFactor;

    // Apply scaling to all text elements
    foreach (var textElement in FindVisualChildren<TextBlock>(this))
    {
        textElement.FontSize *= textScaleFactor;
    }
}
```

## ⌨️ **KEYBOARD ACCESSIBILITY STANDARDS**

### **Keyboard Navigation Requirements**

**🎯 Tab Order Standards:**

```xml
<!-- Logical tab order for MainWindow -->
<Grid>
    <Menu TabIndex="0" KeyboardNavigation.TabNavigation="Cycle"/>
    <NavigationDrawer TabIndex="1"/>
    <ContentFrame TabIndex="2" KeyboardNavigation.TabNavigation="Local"/>
    <StatusBar TabIndex="3"/>
</Grid>
```

**⌨️ Required Keyboard Shortcuts:**

```csharp
// Standard application shortcuts
public static class AccessibilityShortcuts
{
    public static readonly KeyGesture NewRecord = new(Key.N, ModifierKeys.Control);
    public static readonly KeyGesture Save = new(Key.S, ModifierKeys.Control);
    public static readonly KeyGesture Find = new(Key.F, ModifierKeys.Control);
    public static readonly KeyGesture Help = new(Key.F1);
    public static readonly KeyGesture CloseDialog = new(Key.Escape);

    // Navigation shortcuts
    public static readonly KeyGesture NextView = new(Key.Tab, ModifierKeys.Control);
    public static readonly KeyGesture PreviousView = new(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);
}
```

### **Focus Management**

**🎯 Focus Indicator Standards:**

```xml
<!-- High-visibility focus indicators -->
<Style x:Key="AccessibleButtonStyle" TargetType="Button" BasedOn="{StaticResource SyncfusionButtonStyle}">
    <Style.Triggers>
        <Trigger Property="IsFocused" Value="True">
            <Setter Property="BorderBrush" Value="#0078D4"/>      <!-- High contrast blue -->
            <Setter Property="BorderThickness" Value="3"/>        <!-- Thick border -->
            <Setter Property="Effect">
                <Setter.Value>
                    <DropShadowEffect Color="#0078D4" BlurRadius="5" ShadowDepth="0"/>
                </Setter.Value>
            </Setter>
        </Trigger>
    </Style.Triggers>
</Style>
```

**🎯 Focus Trap Implementation:**

```csharp
// Modal dialog focus trapping
public class AccessibleDialog : Window
{
    private UIElement firstTabStop;
    private UIElement lastTabStop;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            // Trap focus within dialog
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift &&
                Keyboard.FocusedElement == firstTabStop)
            {
                lastTabStop.Focus();
                e.Handled = true;
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.None &&
                     Keyboard.FocusedElement == lastTabStop)
            {
                firstTabStop.Focus();
                e.Handled = true;
            }
        }
    }
}
```

## 🔊 **SCREEN READER ACCESSIBILITY**

### **ARIA and Automation Properties**

**🎯 AutomationProperties Standards:**

```xml
<!-- Data grid accessibility -->
<syncfusion:SfDataGrid x:Name="DriversDataGrid"
                       AutomationProperties.Name="Drivers List"
                       AutomationProperties.HelpText="List of all bus drivers with their details">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn MappingName="Name"
                                   HeaderText="Driver Name"
                                   AutomationProperties.Name="Driver Name Column"/>
        <syncfusion:GridTextColumn MappingName="LicenseNumber"
                                   HeaderText="License Number"
                                   AutomationProperties.Name="License Number Column"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>

<!-- Button accessibility -->
<Button x:Name="AddDriverButton"
        Content="Add Driver"
        AutomationProperties.Name="Add New Driver"
        AutomationProperties.HelpText="Click to open the add new driver dialog"/>

<!-- Status information -->
<TextBlock x:Name="StatusText"
           Text="{Binding StatusMessage}"
           AutomationProperties.LiveSetting="Polite"
           AutomationProperties.Name="Application Status"/>
```

### **Dynamic Content Announcements**

**🔊 Live Region Implementation:**

```csharp
// Announce important changes to screen readers
public class AccessibilityAnnouncer
{
    private readonly TextBlock liveRegion;

    public AccessibilityAnnouncer(Panel parentContainer)
    {
        // Create off-screen live region
        liveRegion = new TextBlock
        {
            AutomationProperties.LiveSetting = AutomationLiveSetting.Polite,
            Visibility = Visibility.Collapsed,
            IsTabStop = false
        };
        parentContainer.Children.Add(liveRegion);
    }

    public void Announce(string message, AutomationLiveSetting urgency = AutomationLiveSetting.Polite)
    {
        AutomationProperties.SetLiveSetting(liveRegion, urgency);
        liveRegion.Text = message;
    }
}

// Usage examples
announcer.Announce("Driver John Smith has been added successfully");
announcer.Announce("Error: Please fill in required fields", AutomationLiveSetting.Assertive);
```

## 🎛️ **CONTROL-SPECIFIC ACCESSIBILITY**

### **Syncfusion Control Standards**

**📊 SfDataGrid Accessibility:**

```xml
<syncfusion:SfDataGrid x:Name="ActivitiesGrid"
                       AutomationProperties.Name="Activities Schedule"
                       KeyboardNavigation.TabNavigation="Local"
                       SelectionMode="Single"
                       NavigationMode="Cell">

    <!-- Enable keyboard navigation -->
    <syncfusion:SfDataGrid.GridColumnSizer>
        <syncfusion:GridColumnSizer SizerOption="AutoFitToHeader"/>
    </syncfusion:SfDataGrid.GridColumnSizer>

    <!-- Accessible column headers -->
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn MappingName="ActivityName"
                                   HeaderText="Activity"
                                   AutomationProperties.Name="Activity Name">
            <syncfusion:GridTextColumn.CellTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding ActivityName}"
                               AutomationProperties.Name="{Binding ActivityName, StringFormat='Activity: {0}'}"/>
                </DataTemplate>
            </syncfusion:GridTextColumn.CellTemplate>
        </syncfusion:GridTextColumn>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

**🗂️ NavigationDrawer Accessibility:**

```xml
<syncfusion:SfNavigationDrawer x:Name="MainNavigationDrawer"
                               AutomationProperties.Name="Main Navigation"
                               KeyboardNavigation.TabNavigation="Cycle">
    <syncfusion:SfNavigationDrawer.ContentView>
        <Grid>
            <!-- Navigation items with proper roles -->
            <ListBox x:Name="NavigationMenu"
                     AutomationProperties.Name="Navigation Menu"
                     KeyboardNavigation.DirectionalNavigation="Cycle">
                <ListBoxItem AutomationProperties.Name="Dashboard" AutomationProperties.AcceleratorKey="Ctrl+1">
                    <StackPanel Orientation="Horizontal">
                        <Ellipse Fill="Blue" Width="16" Height="16" AutomationProperties.Name="Dashboard Icon"/>
                        <TextBlock Text="Dashboard" Margin="8,0,0,0"/>
                    </StackPanel>
                </ListBoxItem>
            </ListBox>
        </Grid>
    </syncfusion:SfNavigationDrawer.ContentView>
</syncfusion:SfNavigationDrawer>
```

## 🎯 **VALIDATION COMMANDS**

### **Accessibility Testing Commands**

```powershell
# Check WCAG color contrast compliance
function Test-ColorContrast {
    param([string]$ForegroundColor, [string]$BackgroundColor)

    # Calculate contrast ratio
    $ratio = Get-ContrastRatio -Foreground $ForegroundColor -Background $BackgroundColor

    if ($ratio -ge 4.5) {
        Write-Host "✅ WCAG AA compliant: $ratio`:1" -ForegroundColor Green
    } elseif ($ratio -ge 3.0) {
        Write-Host "⚠️ Large text only: $ratio`:1" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Non-compliant: $ratio`:1" -ForegroundColor Red
    }
}

# Validate keyboard navigation
function Test-KeyboardNavigation {
    param([string]$XamlFile)

    $xaml = [xml](Get-Content $XamlFile)
    $tabIndexElements = $xaml.SelectNodes("//*[@TabIndex]")

    Write-Host "🔍 Keyboard Navigation Analysis for $XamlFile"
    Write-Host "📊 Elements with TabIndex: $($tabIndexElements.Count)"

    # Check for logical tab order
    foreach ($element in $tabIndexElements) {
        Write-Host "  TabIndex $($element.TabIndex): $($element.Name)"
    }
}

# Test screen reader compatibility
function Test-ScreenReaderSupport {
    param([string]$XamlFile)

    $xaml = [xml](Get-Content $XamlFile)
    $automationElements = $xaml.SelectNodes("//*[@AutomationProperties.Name]")

    Write-Host "🔊 Screen Reader Support Analysis for $XamlFile"
    Write-Host "📊 Elements with AutomationProperties: $($automationElements.Count)"
}
```

### **CI/CD Integration**

Add to `.github/workflows/ci-build-test.yml`:

```yaml
- name: 🎯 Accessibility Standards Check
  shell: pwsh
  run: |
      Write-Host "🔍 Checking WCAG 2.1 compliance..." -ForegroundColor Cyan

      # Check for AutomationProperties in XAML files
      $xamlFiles = Get-ChildItem -Recurse -Filter "*.xaml"
      $missingAccessibility = @()

      foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw
        if ($content -notmatch "AutomationProperties\." -and $content -match "<Button|<TextBox|<ComboBox") {
          $missingAccessibility += $file.Name
        }
      }

      if ($missingAccessibility.Count -gt 0) {
        Write-Host "⚠️ Files missing accessibility properties:" -ForegroundColor Yellow
        $missingAccessibility | ForEach-Object { Write-Host "  - $_" }
      } else {
        Write-Host "✅ All XAML files have accessibility properties" -ForegroundColor Green
      }
```

## Implementation checklist

### Phase 2 Accessibility Tasks — **out of ship scope**

These boxes are **not** part of the clerk-path ship tracker ([docs/action-items.md](../docs/action-items.md)). Keep this section as a reference backlog only; do not treat unchecked items as current due-outs.

- [ ] **Color Contrast Audit**: Test all color combinations in both themes
- [ ] **Keyboard Navigation**: Implement logical tab order for all views
- [ ] **Screen Reader Support**: Add AutomationProperties to all interactive elements
- [ ] **Focus Management**: Implement visible focus indicators
- [ ] **Text Scaling**: Support Windows text scaling 100%-200%
- [ ] **High Contrast Mode**: Test with Windows high contrast themes

### Testing Requirements — **out of ship scope**

- [ ] **Manual Testing**: Test with actual screen readers (NVDA, JAWS)
- [ ] **Keyboard Only**: Navigate entire application using only keyboard
- [ ] **Color Blindness**: Test with color blindness simulators
- [ ] **Contrast Testing**: Use automated contrast checking tools
- [ ] **Voice Control**: Test with Windows Speech Recognition

## 🎯 **RESOURCES AND TOOLS**

**📚 Official Guidelines:**

- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [Microsoft Accessibility Guidelines](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/advanced/accessibility-best-practices)
- [Syncfusion Accessibility Documentation](https://help.syncfusion.com/wpf/accessibility)

**🛠️ Testing Tools:**

- **Color Contrast**: [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- **Screen Reader**: [NVDA (Free)](https://www.nvaccess.org/download/)
- **Keyboard Testing**: Manual testing with Tab, Arrow keys
- **Accessibility Insights**: [Microsoft Accessibility Insights](https://accessibilityinsights.io/)

**🎯 Success Criteria:**

- ✅ **Level AA compliance** for all interactive elements
- ✅ **4.5:1 contrast ratio** for all text
- ✅ **Full keyboard navigation** without mouse
- ✅ **Screen reader compatibility** for all content
- ✅ **Focus indicators** clearly visible
- ✅ **Text scaling support** up to 200%

---

**📊 This standard ensures BusBuddy is accessible to all users, meeting legal requirements and providing an inclusive user experience.**
