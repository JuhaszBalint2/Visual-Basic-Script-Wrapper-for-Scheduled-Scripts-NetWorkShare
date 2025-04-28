# Codebase Summary

## Key Components and Their Interactions
- **ScriptSchedulerApp (C# Project)**: Contains the core logic for the application.
    - **TaskWrapper.cs**: Generates VBS wrappers for network scripts, handles hidden execution.
    - **TaskCreator.cs**: Creates and registers tasks in Windows Task Scheduler using the TaskScheduler library. Replaces previous PowerShell scripts.
    - **MainWindow.xaml/cs**: The main user interface (assuming WPF based on file list).
    - **ScriptManager.cs**: Manages script-related operations.
    - **CredentialManagerHelper.cs**: Handles interaction with Windows Credential Manager.
    - **TaskSchedulerExtensions.cs**: Provides extension methods for enhanced Windows Task Scheduler integration.
    - **app.manifest**: Controls application permissions and OS compatibility requirements.
- **VBSWrapper (Directory)**: Default location for generated VBS wrappers and their logs.

## Data Flow
1. User inputs script details (path, type, args, working dir, credentials) via the UI or command line.
2. `TaskWrapper.cs` generates a VBS wrapper script in the specified location (e.g., `VBSWrapper` directory).
3. `TaskCreator.cs` uses the TaskScheduler library to create and register a scheduled task.
    - The task action is configured to run the generated VBS wrapper (`wscript.exe path\to\wrapper.vbs`).
    - Credentials (if provided) are handled via the Task Scheduler's built-in mechanisms or potentially retrieved via `CredentialManagerHelper.cs` if needed for the wrapper itself (though typically the task runs as the specified user).
4. When triggered by the schedule, Windows Task Scheduler executes the VBS wrapper.
5. The VBS wrapper executes the target script (PowerShell, Python, Batch, etc.) hidden, using the full path provided during creation.
6. The VBS wrapper logs execution start, command, user, and exit code to a file (currently hardcoded to `C:\Temp`).

## External Dependencies
- **.NET 6.0 (or later)**: Runtime for the C# application.
- **TaskScheduler Library**: NuGet package used for interacting with Windows Task Scheduler API.
- **Windows Task Scheduler Service**: Required for tasks to run.
- **Windows Script Host (WScript.exe)**: Required to execute the generated VBS wrappers.
- **Target Script Runtimes**: (e.g., PowerShell, Python) must be installed on the target machine where the task runs.
- **Administrative privileges**: Required for task creation/modification.
- **Windows 10 or Windows 11**: Application is configured to run only on these operating systems.

## Recent Significant Changes
- **Updated App Manifest Configuration**:
  - Modified app.manifest to only support Windows 10 and Windows 11
  - Ensured application always runs with administrator privileges
  - Added proper theme dependency for Windows common controls
  - Verified manifest is referenced correctly in project file

- **Fixed Compiler Errors and UI Control Issues**:
  - Created missing EqualityToVisibilityConverter class for XAML visibility binding
  - Added email-related UI controls (EmailActionPanel, EmailFromTextBox, etc.)
  - Renamed ambiguous TaskSchedulerExtensions.AddEmailAction method to CreateEmailAction
  - Fixed MonthlyTrigger and MonthlyDOWTrigger to use correct MonthsOfYear property
  - Created detailed documentation in BugFixesApplied.md

- **Fixed C# File Structure Issues in Multiple Files**:
  - Reorganized MainWindow.xaml.cs file to follow proper C# structure
  - Fixed TaskSchedulerExtensions.cs file structure and method placement
  - Moved all `using` statements to the top of all source files
  - Fixed method placement to ensure all code is within proper class contexts
  - Corrected invalid modifiers (both 'private' and 'public')
  - Created comprehensive documentation in CSFileFixesApplied.md

- **Fixed XAML Implementation Issues (Reapplied)**:
  - Corrected all mismatched XML tags in MainWindow.xaml
  - Fixed Border element issues (each Border can only have one child)
  - Eliminated duplicate control definitions, particularly in the WeeklySchedulePanel
  - Properly structured all XML elements to maintain correct hierarchy
  - Created comprehensive documentation in XAMLFixesReapplied.md

- **Previous UI Reference Issues Fixed**:
  - Added missing name attribute for the InstancePolicyComboBox in MainWindow.xaml
  - Fixed reference to RunWithHighestPrivilegesCheckBox in MainWindow.xaml.cs
  - Added converters namespace to MainWindow.xaml to properly reference EqualityToVisibilityConverter
- **Complete Migration to C#**: Replaced all PowerShell-based logic with C# implementation.
- **CodeDom Compilation**: Implemented for potential C# wrapper generation (though VBS is primary).
- **VBS Wrapper Fixes**:
    - Corrected semicolon issue in `fso.CreateFolder`.
    - Fixed UNC path error by commenting out `WshShell.CurrentDirectory` setting.
- **Cleanup of Obsolete Scripts**: Removed old `.ps1` and `.bat` files from the project root.
- **Windows Credential Manager Integration**: Added support for retrieving credentials.
- **UI Button Repositioning**: Moved navigation buttons ("Back", "Next", "Create Task") to a dedicated row below the main content area in `MainWindow.xaml` for improved layout.
- **Enhanced Windows Task Scheduler Integration**:
    - Added `TaskSchedulerExtensions.cs` to provide comprehensive Windows Task Scheduler support
    - Implemented full support for all trigger types (One-time, Daily, Weekly, System Startup, Log On)
    - Added support for power settings, network conditions, and idle settings
    - Enhanced security and execution options

## User Feedback Integration
- **VBS UNC Path Error Fixed**: Addressed the "Invalid path specified" error when using UNC paths by modifying VBS generation.
- **Path Handling**: Previous issues with spaces in paths were addressed in C# logic and VBS generation quoting.
- **Error Handling**: Ongoing improvements, VBS wrapper includes basic logging.
- **Administrative Privileges**: Application now ensures it always runs with required privileges for task creation.