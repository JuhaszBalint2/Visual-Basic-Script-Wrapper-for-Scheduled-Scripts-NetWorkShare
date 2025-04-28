# Technology Stack

## Core Technologies
- PowerShell 5.1+ - Primary scripting language for task management and script generation
- VBScript - Used for script wrappers to handle network paths and encodings
- Windows Task Scheduler - For scheduling task execution

## Development Tools
- PowerShell ISE/VSCode - For script development and testing
- Windows Task Scheduler UI - For verification of created tasks

## Key Components
- VBS Wrapper - Provides consistent script execution environment on network shares
- PowerShell Task Creation - Abstracts the complexity of schtasks.exe
- UTF-8 Encoding - Ensures proper character handling for international characters

## Architecture Decisions
- VBS used as wrapper due to its native Windows support and COM object access
- PowerShell automation for all task creation and management
- Separate local VBS wrappers pointing to network scripts for better reliability
- Robust error handling with detailed logging
