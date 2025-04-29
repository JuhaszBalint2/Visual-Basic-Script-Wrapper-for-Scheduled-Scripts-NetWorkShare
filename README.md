# Script Scheduler Application

A Windows application for scheduling scripts to run as Windows Tasks, with complete feature parity with Windows Task Scheduler and support for wrapping scripts in standalone executables.

## Major Changes

This version introduces comprehensive Windows Task Scheduler feature parity:

- **Full Task Scheduler Integration**: Supports all Windows Task Scheduler trigger types and actions
- **Pure C# Implementation**: Completely removed all PowerShell and VBS script dependencies
- **C# Script Wrapper**: Generates C# executable wrappers instead of VBS scripts
- **Command-line Interface**: Added a command-line interface to replace PowerShell scripts
- **Unified Code Base**: All functionality is now contained in the C# project

## Features

### Script Support
- Create and schedule Windows Tasks for various script types:
  - PowerShell (.ps1)
  - Python (.py)
  - Batch (.bat, .cmd)
  - VBScript (.vbs)
  - JavaScript (.js)
  - Executable (.exe)

### Script Wrappers
- Create C# wrappers that:
  - Run scripts with hidden windows
  - Provide detailed logging
  - Handle errors gracefully
  - Work with network paths

### Comprehensive Trigger Types
- **On a schedule**:
  - One-time
  - Daily
  - Weekly 
  - Monthly (day of month and day of week options)
- **System events**:
  - At system startup
  - At logon
  - On idle
  - On an event (from event logs)
  - At task creation/modification
- **Session state changes**:
  - Connect/Disconnect
  - Remote Connect/Disconnect
  - Lock/Unlock

### Multiple Action Types
- Run a program or script
- Send email notifications
- Display message

### Advanced Configuration Options
- Task execution settings:
  - Run with highest privileges
  - Run whether user is logged on or not
  - Do not store password
  - Multiple instances policy (parallel, queue, stop existing)
  - OS compatibility options

- Run conditions:
  - Power settings (on battery, wake to run)
  - Network conditions
  - Idle conditions
  
- Task management:
  - Auto-restart on failure
  - Custom execution time limits
  - Repetition patterns

## Usage

### GUI Mode

1. Run `ScriptSchedulerApp.exe` without arguments to open the GUI
2. Select a script, configure schedule options, and create a task

### Command Line Mode

#### Create a C# Wrapper

```
ScriptSchedulerApp.exe create-wrapper --script=C:\Scripts\MyScript.ps1 --output=C:\Wrappers\MyScript.exe
```

#### Create a Scheduled Task

```
ScriptSchedulerApp.exe create-task --name="My Task" --script=C:\Scripts\MyScript.ps1 --trigger=DAILY --interval=1
```

## Building

Run `Build.bat` to build the application. The executable will be in the `ScriptSchedulerApp\bin\Release\net6.0-windows` directory.

Alternatively, use Visual Studio or the .NET CLI:

```
dotnet build ScriptSchedulerApp\ScriptSchedulerApp.csproj -c Release
```

## Requirements

- Windows 10/11 or Windows Server 2016/2019/2022
- .NET 6.0 Runtime
- Administrator privileges (required for task creation)
