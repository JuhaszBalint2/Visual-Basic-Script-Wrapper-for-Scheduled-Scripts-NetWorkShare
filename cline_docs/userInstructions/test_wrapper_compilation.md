# Testing the Fixed Wrapper Compilation

This document provides instructions for testing the updated wrapper compilation feature that addresses the "Failed to create wrapper" error.

## Build the Application

1. Close any running instances of the application
2. Run the `Build.bat` script in the project root directory or use Visual Studio to rebuild the solution
3. Wait for the build to complete successfully

## Test Scenario 1: GUI-Based Wrapper Creation

1. Run the compiled application (`ScriptSchedulerApp.exe` in the `bin\Release\net6.0-windows` directory)
2. Select a PowerShell or batch script from your network share
3. Enter a task name and configure schedule options
4. **Important**: Check the "Create C# Wrapper" option
5. Set a wrapper location (preferably on a network share with spaces in the path like `\\server\Shared Folder\Wrappers`)
6. Click "Create Task"
7. The task should be created successfully without any "Failed to compile wrapper" errors
8. Verify that the wrapper executable was created in the specified location

## Test Scenario 2: Command-Line Wrapper Creation

1. Open a command prompt
2. Navigate to the application directory
3. Run the following command (adjust paths as needed):
   ```
   ScriptSchedulerApp.exe create-wrapper --script="\\server\Shared Folder\Scripts\test.ps1" --output="\\server\Shared Folder\Wrappers\test_wrapper.exe"
   ```
4. The wrapper should be created successfully
5. Verify the wrapper exists in the specified output location

## Test Scenario 3: Command-Line Task Creation with Wrapper

1. Open a command prompt
2. Navigate to the application directory
3. Run the following command (adjust paths as needed):
   ```
   ScriptSchedulerApp.exe create-task --name="Test Task With Wrapper" --script="\\server\Shared Folder\Scripts\test.ps1" --wrapper=true
   ```
4. The task should be created successfully with a wrapper
5. Check the Windows Task Scheduler to verify the task was created
6. Run the task manually to test execution

## Troubleshooting

If you encounter issues:

1. **Check the logs:**
   - Look for wrapper compilation error details in the debug output or application logs
   - The new compiler uses CodeDom so errors will be more detailed

2. **Verify references:**
   - Make sure the System.CodeDom NuGet package was properly added to the project
   - The project may need additional references depending on your environment

3. **Additional debugging:**
   - Running the application with a debugger attached will provide more detailed information
   - Set breakpoints in the TaskWrapper.cs file to see exactly where any issues occur

## Reporting Issues

If you encounter any issues that aren't resolved by the troubleshooting steps:

1. Document the exact error message
2. Note the environment details (OS version, .NET version)
3. Describe the steps to reproduce the issue
4. Share any relevant logs or screenshots
