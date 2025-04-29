# Windows Task Scheduler Features Guide

This guide covers all the Windows Task Scheduler features now available in the Script Scheduler application.

## Trigger Types

### Schedule-based Triggers

* **One-time**: Run the task once at a specified date and time
* **Daily**: Run the task every day or every X days
* **Weekly**: Run the task on specific days of the week
* **Monthly**: Two options available:
  * Day of month: Run on specific days (e.g., 1st, 15th) of selected months
  * Day of week: Run on specific occurrences of days (e.g., third Monday) of selected months

### Event-based Triggers

* **At system startup**: Run when the computer starts
* **At log on**: Run when a user logs on
* **On idle**: Run when the computer has been idle for a specified period
* **On an event**: Run when a specific event is logged in Windows Event Log
* **At task creation/modification**: Run when the task is registered or modified
* **On session change**: Run in response to session events such as:
  * Connect/Disconnect
  * Remote Connect/Disconnect
  * Lock/Unlock

## Action Types

* **Run program**: Execute a program, script, or command with specified arguments
* **Send email**: Send an email notification with configurable SMTP settings
* **Display message**: Show a popup message to the user

## Advanced Settings

### Basic Settings

* **Allow task to be run on demand**: Enable manual triggering of the task
* **Run task as soon as possible after a missed scheduled start**: Run the task if the computer was off at scheduled time
* **Restart on failure**: Configure automatic restart if the task fails

### Multiple Instances Policy

* **Do not start a new instance**: Skip if already running
* **Run a new instance in parallel**: Run multiple instances simultaneously
* **Stop the existing instance**: Terminate existing instance and start new one
* **Queue a new instance**: Wait for current instance to finish

### Execution Time Limit

* Configure maximum runtime for the task
* Option for unlimited runtime

### Power Settings

* **Stop if computer switches to battery power**: Halt execution when unplugged
* **Start only if computer is on AC power**: Don't run on battery
* **Wake computer to run task**: Power up from sleep to execute

### Network Settings

* **Run only when network connection is available**: Ensure connectivity before execution

### Idle Settings

* **Start only if computer is idle for a specified period**: Wait for inactivity
* Configure how long to wait for idle state

### Security Options

* **Run with highest privileges**: Elevate to administrator rights
* **Run whether user is logged on or not**: Execute even without active user session
* **Do not store password**: More secure, but limits network resource access
* **Store password using reversible encryption**: Required for domain accounts and network resources

### OS Compatibility

* Configure tasks to be compatible with specific Windows versions:
  * Windows 10 / Server 2016+
  * Windows 8 / Server 2012
  * Windows 7 / Server 2008 R2
  * Windows Vista / Server 2008
  * Windows XP / Server 2003

## Repetition Settings

Available for most trigger types to run a task multiple times after being triggered:

* **Interval**: How frequently to repeat (minutes or hours)
* **Duration**: How long to continue repeating
* **Stop at end of duration**: Whether to cancel if duration ends

## Best Practices

1. **Use descriptive task names** that indicate purpose and function
2. **Add detailed descriptions** to document the task's purpose
3. **Test triggers and actions** before deploying to production
4. **Consider security implications** when running tasks as specific users
5. **Use VBS wrappers** for scripts that need to run hidden
6. **Implement error handling** in your scripts to make troubleshooting easier
7. **Configure appropriate restart policies** for critical tasks
8. **Monitor task history** for failures and unexpected behavior

## Frequently Asked Questions

### Why use a wrapper script instead of running my script directly?

Wrapper scripts provide several benefits:
- Run scripts completely hidden with no console window
- Provide consistent error handling and logging
- Solve network path access issues in scheduled tasks
- Ensure proper character encoding

### What is the "Run whether user is logged on or not" option?

This allows the task to run even when no user is logged into the computer. This is useful for server tasks and background processes. Note that when using this option:
- The task runs in a non-interactive session
- You must provide stored credentials (unless using SYSTEM account)
- Network resource access may be affected depending on security settings

### How do I troubleshoot a task that isn't running?

1. Check the task's Last Run Result in Task Scheduler
2. Review history in the Task Scheduler's History tab
3. Examine wrapper script logs in the designated log folder
4. Verify security settings and credentials are correct
5. Test running the task manually using "Run" in Task Scheduler

### When should I use "On idle" vs "On a schedule"?

- **On idle**: Best for non-critical maintenance tasks that should only run when the computer isn't being actively used
- **On a schedule**: Better for critical tasks that must run at specific times regardless of user activity

### How do I set up email notifications?

1. Select "Send Email" as the action type
2. Configure the email settings:
   - From/To addresses
   - Subject and message
   - SMTP server details and security options
3. Ensure your SMTP server is accessible from the computer running the task

### What's the difference between TaskRunLevel settings?

- **Highest privileges (Administrator)**: Task runs with full administrative rights
- **Limited (User)**: Task runs with standard user rights

### How do multiple actions work?

You can add multiple actions to a single task. These actions are executed sequentially in the order they appear in the task definition. For example, you could:
1. Run a script to process data
2. Send an email with the results
3. Display a message to notify users

## Advanced Scenarios

### Running tasks based on event logs

You can trigger tasks based on specific events in Windows Event Logs, useful for:
- Responding to system errors
- Monitoring application logs
- Triggering actions based on security events

To configure:
1. Select "On an event" trigger type
2. Specify the log (System, Application, Security, etc.)
3. Optionally filter by source and event ID

### Using session state changes

Session state triggers are useful for:
- Setting up user environments at logon
- Cleaning up resources at logoff
- Securing workstations at lock/unlock events

### Implementing task sequences

While Task Scheduler doesn't directly support dependencies between tasks, you can create task sequences by:
1. Using the "At task creation/modification" trigger for the first task
2. Having each task in the sequence create or modify the next task
3. Using exit codes to determine whether to proceed

## Script Wrapper Options

When creating script wrappers, you can configure several options:

### Wrapper Type

- **VBScript (.vbs)**: Compatible with all Windows versions, runs scripts hidden
- **C# Executable (.exe)**: More powerful, can implement complex logic

### Logging Options

- **Log folder**: Where execution logs are stored
- **Log level**: Amount of detail recorded in logs
- **Log format**: Structure and content of log entries

### Execution Environment

- **Working directory**: Starting folder for script execution
- **Script arguments**: Parameters passed to the script
- **Character encoding**: How text in scripts is interpreted

## Command-Line Interface

For automation and integration, you can use the command-line interface:

### Creating a wrapper:
```
ScriptSchedulerApp.exe create-wrapper --script="path\to\script.ps1" --output="path\to\wrapper.vbs"
```

### Creating a scheduled task:
```
ScriptSchedulerApp.exe create-task --name="Task Name" --script="path\to\script.ps1" --trigger=DAILY --interval=1
```

### Advanced options:
```
ScriptSchedulerApp.exe create-task --name="Task Name" --script="path\to\script.ps1" --trigger=WEEKLY --days=MON,WED,FRI --create-wrapper --run-as=SYSTEM --highest-privileges
```

## Troubleshooting Common Issues

### Task doesn't run at scheduled time

- Verify the computer is powered on at the scheduled time
- Check if "Run task as soon as possible after a scheduled start is missed" is enabled
- Ensure the configured user account has necessary permissions
- Verify the task is enabled and not expired

### Scripts fail with access denied errors

- Check if the script needs administrator privileges
- Verify network path permissions if accessing network resources
- Ensure the user account has access to all required resources
- Consider using stored credentials for network access

### Email notifications aren't being sent

- Verify SMTP server settings are correct
- Check for firewall restrictions blocking SMTP traffic
- Ensure the sender email address is valid
- Consider using SSL/TLS if required by your mail server

### Display messages don't appear

- Verify the task is running in interactive mode (not "Run whether user is logged on or not")
- Check if the message is being displayed on a different session
- Ensure the task is running with appropriate user context

## Additional Resources

- [Microsoft Task Scheduler Documentation](https://docs.microsoft.com/en-us/windows/win32/taskschd/task-scheduler-start-page)
- [PowerShell Task Scheduler cmdlets](https://docs.microsoft.com/en-us/powershell/module/scheduledtasks/)
- [Task Scheduler security considerations](https://docs.microsoft.com/en-us/windows/security/threat-protection/security-policy-settings/task-scheduler)
