# Script Scheduler Application

A Windows application for scheduling scripts to run as Windows Tasks, with support for wrapping scripts in standalone executables.

## Major Changes

This version introduces significant architectural improvements:

- **Pure C# Implementation**: Completely removed all PowerShell and VBS script dependencies
- **C# Script Wrapper**: Now generates C# executable wrappers instead of VBS scripts
- **Command-line Interface**: Added a command-line interface to replace PowerShell scripts
- **Unified Code Base**: All functionality is now contained in the C# project

## Features

- Create and schedule Windows Tasks for various script types:
  - PowerShell (.ps1)
  - Python (.py)
  - Batch (.bat, .cmd)
  - VBScript (.vbs)
  - JavaScript (.js)
  - Executable (.exe)
- Create C# wrappers that:
  - Run scripts with hidden windows
  - Provide detailed logging
  - Handle errors gracefully
  - Work with network paths
- Multiple trigger types:
  - One-time
  - Daily
  - Weekly
  - At startup
  - At logon
- Configure various task options:
  - Run with highest privileges
  - Run as specific user (with Windows Credential Manager integration)
  - Set execution limits and restart policies

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
- Microsoft C# Compiler (comes with .NET SDK)
