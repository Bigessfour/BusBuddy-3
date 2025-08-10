---
applyTo: '**'
---
# BusBuddy Project Guidelines

**Purpose**: Guides developers and AI assistants on BusBuddy MVP goals, project context, and development workflows. For technical standards, compliance, and anti-regression rules, see `.github/copilot-instructions.md`.

**Scope**: Defines what to build (student entry, ro#### 💬 **Communication Preferences**
- **Concise responses** - conserve time with brief, focused answers
- **One-issue-at-a-time** approach for fixing build errors
- **Use PowerShell commands** not raw dotnet commands
- **Direct action** - fix issues immediately using available tools

### Debugging & Troubleshooting
For technical error resolution (e.g., CS0103, CS0246), see `.github/copilot-instructions.md`. Use these steps for project-specific issues:
- Run `bbHealth` to diagnose environment issues.
- Check build output with `bbBuild` for detailed error information.
- Use `bbRun` for runtime logging and testing.
- Export diagnostics with available PowerShell functions.

#### 🔧 **Problem Resolution Approach**ignment) and how to achieve a runnable MVP with minimal build errors.

## 🎯 **Project Status & Roadmap**

### Current Status: MVP Acceleration (Phase 2)
- **Goal**: Achieve a clean build (0 errors) and deliver a runnable application with student entry and route assignment by end-of-day.
- **Progress**: Reduced build errors from 87 to 4 by disabling non-MVP services (e.g., XAI, GoogleEarthEngine). PowerShell route demo (`bb-route-demo`) is fully operational.
- **Blockers**: 4 CS0246 errors in `AIEnhancedRouteService.cs` (to be resolved by disabling).

**MVP Priorities**:
- **Student Entry**: Form for adding students (name, address, phone, grade) with basic validation.
- **Route Assignment**: UI for assigning students to buses/drivers, generating schedules.
- **UI**: Syncfusion-based forms (`StudentsView.xaml`, `StudentForm.xaml`, `RoutesView.xaml`).
- Command works consistently every time without prompts
- Application launches as WPF desktop application  
- Dashboard window should appear
- Syncfusion controls should be properly initialized
- No console window should remain open
### BusBuddy Domain Knowledge

#### 🚌 **Core Business Context**
- **School transportation management system** for bus fleet operations
- **Key entities**: Vehicles, Drivers, Routes, Maintenance, Fuel, Activities
- **Primary users**: Transportation coordinators, mechanics, administrators
- **Critical features**: Safety compliance, route optimization, maintenance tracking

#### 🎨 **UI/UX Standards**
- **Syncfusion controls preferred** over standard Windows Forms
- **Material Design theming** with consistent color schemes
- **Responsive layouts** that handle DPI scaling properly
- **Enhanced dashboards** with diagnostic logging and fallback strategies

#### 📚 **Syncfusion Essential Windows Forms Guidelines**

Based on [Syncfusion Windows Forms Overview](https://help.syncfusion.com/windowsforms/overview):

**Core Principles:**
- **100+ Essential Controls** - Comprehensive collection for enterprise applications
- **Performance-first** - Unparalleled performance and rendering
- **Touch-friendly UI** - Modern, responsive interface design
- **Built-in themes** - Professional, consistent visual styling
- **Visual Studio integration** - Seamless development experience

**Key Control Categories:**
- **Data Visualization**: Chart, Diagram, Maps, Gauges, TreeMap, Sparkline
- **Data Management**: DataGrid, Grid Control, Pivot Grid, GridGroupingControl
- **Navigation**: Docking Manager, TabControl, TreeView, Navigation Drawer, Ribbon
- **Layout Management**: Border Layout, Flow Layout, Grid Layout, Tile Layout
- **Input Controls**: MaskedTextBox, AutoComplete, ColorPicker, DateTimePickerAdv
- **File Handling**: PDF Viewer, Spreadsheet, Syntax Editor, HTML Viewer

**BusBuddy-Specific Usage:**
- **Docking Manager**: Primary dashboard layout with professional docking
- **DataGrid**: Vehicle, driver, and route data management
- **Chart Controls**: Fleet analytics and performance visualization  
- **Scheduler**: Route planning and maintenance scheduling
- **TabControl**: Multi-module interface organization
- **TreeView**: Hierarchical data navigation (routes, organizations)

**Development Best Practices:**
1. **Getting Started**: Always refer to component-specific "Getting Started" guides
2. **Code Examples**: Use sample browser with hundreds of code examples
3. **API Reference**: Detailed object hierarchy and settings documentation
4. **Search First**: Use search functionality for specific features
5. **Licensing**: Proper license key registration to avoid runtime dialogs

**Resource Utilization:**
- **Knowledge Base**: Common questions and solutions
- **Community Forums**: Peer support and discussions  
- **Support Tickets**: Direct technical assistance
- **Feedback Portal**: Feature requests and suggestions

**Version Compatibility:**
- **Target .NET 8.0+** (Support for .NET 6.0/7.0 discontinued in 2025 Volume 1)
- **Regular updates** following Syncfusion release cycles
- **Backward compatibility** considerations for existing components

#### 💾 **Data Architecture**
- **Entity Framework Core** for data access
- **Repository pattern** with dependency injection
- **SQL Server backend** with proper connection management
- **Test database initialization** for development/testing

#### 🧪 **Testing Approach**
- **UI tests** in `BusBuddy.Tests/UI/` directory
- **Integration tests** for data layer operations
- **Coverage reports** generated via PowerShell scripts
- **Avoid legacy test files** - consolidate into organized structure

#### 🔧 **Development Tools**
- **PowerShell (pwsh)** for ALL commands - use abbreviated pwsh when possible
- **Single build approach** - run one build, get data, move on - no repetitive builds
- **VS Code tasks** for build/test operations
- **Syncfusion licensing** handled via helper classes
- **Git hooks** for code quality enforcement

**⚠️ KNOWN TECHNICAL DEBT (Post-MVP Cleanup Required):**
- **PowerShell Modules**: Current modules may not fully comply with Microsoft standards
- **Write-Host Usage**: Some PowerShell scripts use Write-Host instead of proper output streams
- **Module Structure**: Large monolithic modules need refactoring per Microsoft guidelines
- **Documentation Links**: Not all Syncfusion implementations include official documentation references

**MVP Approach**: These items don't block MVP functionality but must be addressed in Phase 2

#### � **Communication Preferences**
- **Concise responses** - conserve time with brief, focused answers
- **One-issue-at-a-time** approach for fixing build errors
- **Use PowerShell commands** not raw dotnet commands
- **Direct action** - fix issues immediately using available tools

#### 🔧 **Problem Resolution Approach**
- **Incremental fixes first** - Always attempt targeted edits before complete rebuilds
- **Assess corruption level** - Check actual errors and their scope before deciding approach
- **User consultation** - Let the user decide on complete overhauls vs. targeted fixes
- **Error analysis** - Identify root causes (missing methods, property name mismatches, duplicates)
- **Minimal viable fix** - Use the smallest change that resolves the issue
- **Escalation path**: 
  1. First: Targeted edits for specific errors
  2. Second: Consult user if issues appear complex
  3. Last resort: Complete rebuild only with user approval

#### 🏗️ **Syncfusion Implementation Requirements**

**CRITICAL RULE: Only Use Official Syncfusion Documentation**
- **Reference ONLY**: https://help.syncfusion.com/cr/windowsforms/Syncfusion.html
- **No custom fixes**: Use only documented Syncfusion APIs, methods, and examples
- **No invented code**: Every Syncfusion implementation must be found in official docs
- **No modifications** to documented patterns without verifying in docs

**Documentation-First Development Process:**
1. **Search Syncfusion docs** for the specific control/feature needed
2. **Find official examples** in the documentation or sample browser
3. **Copy exact patterns** from Syncfusion's documented examples
4. **Test with documented parameters** and properties only
5. **No modifications** to documented patterns without verifying in docs

**Common Syncfusion Controls - Documentation Required:**
- **RibbonControlAdv**: Use Header.AddMainItem() for tabs (documented pattern)
- **DockingManager**: Use DockingStyle enum for docking operations
- **TileLayout**: Use LayoutGroup and HubTile as per documentation
- **SfDataGrid**: Follow documented binding and column configuration patterns
- **ChartControl**: Use only documented series types and properties
- **SfButton**: Apply only documented style properties and themes

**Forbidden Practices:**
- ❌ **NO custom Syncfusion extensions** or helper methods
- ❌ **NO invented property combinations** not shown in docs
- ❌ **NO assumed API patterns** based on other frameworks
- ❌ **NO "enhanced" wrappers** around Syncfusion controls
- ❌ **NO undocumented parameters** or method calls

**Required Verification Steps:**
1. **Before any Syncfusion code**: Search help.syncfusion.com for exact usage
2. **Cross-reference examples**: Find matching code in Syncfusion's sample browser
3. **API validation**: Verify all properties/methods exist in official API reference
4. **Documentation links**: Include reference to specific Syncfusion doc page used

**Example of Correct Documentation-Based Implementation:**
```csharp
// Based on official Syncfusion RibbonControlAdv documentation
var tabItem = new TabHost
{
    Text = "Dashboard"
};
_ribbonControl.Header.AddMainItem(tabItem); // Documented method

// Based on official DockingManager documentation  
_dockingManager.DockControl(panel, this, DockingStyle.Left, 280); // Documented enum
```

---

## 🎯 **MVP Completion Criteria & Goal Transition**

### **MVP Success Validation Checklist**

When ALL items below are verified as working, the MVP phase is COMPLETE:

#### **✅ Core Functionality Validation**
- [ ] **Student Entry Form**: Complete form with all fields (name, address, phone, grade) saving to database
- [ ] **Address Validation**: Real-time validation with error messages for invalid addresses
- [ ] **Student List View**: SfDataGrid displaying all students with search/filter capabilities
- [ ] **Route Assignment**: Dropdown selection linking students to specific buses and drivers
- [ ] **Route Schedule Display**: Grid showing complete route assignments (Student → Bus → Driver → Time)
- [ ] **Data Persistence**: All data saves correctly to database and persists between application restarts

#### **✅ Technical Performance Validation**
- [ ] **Application Startup**: Launches in under 10 seconds with no error dialogs
- [ ] **Form Responsiveness**: All forms load instantly with smooth user interactions
- [ ] **Database Operations**: All CRUD operations complete in under 2 seconds
- [ ] **Error Handling**: Graceful error messages for all failure scenarios
- [ ] **Memory Stability**: Application runs for 30+ minutes without memory leaks
- [ ] **Build Reliability**: `dotnet build` succeeds consistently with 0 errors, 0 warnings

#### **✅ User Experience Validation**
- [ ] **Intuitive Navigation**: Users can navigate between all views without training
- [ ] **Data Entry Flow**: Complete student-to-route workflow takes under 5 minutes
- [ ] **Visual Consistency**: All UI elements use Syncfusion FluentDark theme consistently
- [ ] **Validation Feedback**: Clear, actionable error messages for all validation failures
- [ ] **Export Functionality**: Route assignments export to readable format (CSV/Excel)

#### **✅ Operational Readiness**
- [ ] **Sample Data**: 15-20 students, 5-8 drivers, 3-5 vehicles, 10+ route assignments
- [ ] **Documentation**: Updated README with setup and usage instructions
- [ ] **Test Coverage**: All new features covered by automated tests (85%+ coverage)
- [ ] **Deployment Package**: Runnable executable with all dependencies included

### **🚀 When MVP is Complete: Goal Transition Protocol**

#### **Step 1: Final Validation (Required Before Transition)**
```powershell
# Run complete validation suite
dotnet clean && dotnet build
dotnet test --verbosity normal
dotnet run --project BusBuddy.WPF

# Verify all MVP checklist items manually
# Document any remaining issues in GitHub Issues
```

#### **Step 2: Success Declaration**
When ALL MVP criteria are verified:

**🎉 DECLARE MVP SUCCESS**: 
> "BusBuddy MVP Phase COMPLETE! All core functionality validated. Ready for Phase 2 planning."

#### **Step 3: Phase 2 Goal Definition Process**

**Stakeholder Review Requirements:**
1. **Demo the working MVP** to key stakeholders
2. **Gather feedback** on user experience and feature gaps
3. **Prioritize Phase 2 features** based on operational impact
4. **Define success metrics** for next development cycle

**Phase 2 Goal Setting Template:**
```markdown
## Phase 2 Goals: [Feature Name] Enhancement

**Primary Objective**: [Specific business outcome]
**Success Criteria**: [Measurable validation criteria]
**Timeline**: [Development duration estimate]
**Dependencies**: [Technical or business prerequisites]
**Definition of Done**: [Specific completion checklist]
```

**Recommended Phase 2 Focus Areas:**
1. **Route Optimization**: Algorithmic route planning with distance/time optimization
2. **Google Earth Integration**: Visual route mapping and address geocoding
3. **Performance Enhancement**: Database optimization and UI responsiveness improvements
4. **Advanced Reporting**: Operational analytics and compliance reporting
5. **Mobile Companion**: Driver mobile app for route management

#### **Step 4: Documentation-First Transition**
**CRITICAL**: Once MVP is complete, activate full documentation-first development:

- [ ] **Review copilot-instructions.md compliance** - Assess current code against Microsoft standards
- [ ] **PowerShell Module Cleanup** - Address any Write-Host violations and module structure issues
- [ ] **Syncfusion Documentation Audit** - Ensure all UI controls use officially documented patterns only
- [ ] **Technical Debt Documentation** - List all MVP shortcuts taken for systematic cleanup
- [ ] **Code Review Standards** - Implement documentation-first validation in PR process
- [ ] **Team Training** - Ensure all developers understand documentation-first requirements

**Documentation-First Checklist:**
- [ ] All PowerShell follows Microsoft PowerShell Development Guidelines
- [ ] All Syncfusion implementations reference official documentation with links
- [ ] All .NET code patterns follow official .NET documentation
- [ ] No "assumed" patterns or "quick fixes" without documentation verification
- [ ] All code comments include documentation sources where applicable

#### **Step 5: New Goal Activation**
- [ ] **Update BusBuddy.instructions.md** with new Phase 2 goals
- [ ] **Create GitHub milestone** for Phase 2 features
- [ ] **Establish new success criteria** using the same validation framework
- [ ] **Update development timeline** with realistic Phase 2 estimates

### **🔄 Continuous Improvement Cycle**

**After each major phase completion:**
1. **Retrospective**: What worked well? What should improve?
2. **Architecture Review**: Technical debt assessment and refactoring priorities
3. **User Feedback Integration**: Real-world usage insights and feature requests
4. **Technology Evaluation**: New tools, frameworks, or approaches to consider
5. **Goal Refinement**: Adjust future phases based on lessons learned

---

**Remember**: MVP completion is not the end—it's the foundation for building a world-class school transportation management system that truly serves students, parents, drivers, and administrators.

---

**Last Updated**: August 2, 2025 - MVP Acceleration Plan with Completion Criteria
**Last Updated**: August 2, 2025 - MVP Acceleration Plan with Completion Criteria
**Remember**: MVP completion is not the end—it's the foundation for building a world-class school transportation management system that truly serves students, parents, drivers, and administrators.

---

**Last Updated**: August 2, 2025 - MVP Acceleration Plan with Completion Criteria
**Last Updated**: August 2, 2025 - MVP Acceleration Plan with Completion Criteria
