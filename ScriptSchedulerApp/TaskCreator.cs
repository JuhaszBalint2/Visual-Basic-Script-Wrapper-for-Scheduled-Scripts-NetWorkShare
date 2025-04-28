using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32.TaskScheduler;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Standalone C# class for creating Windows Scheduled Tasks
    /// Replaces the PowerShell DirectTaskCreator.ps1 and FixedTaskCreator.ps1 scripts
    /// </summary>
    public class TaskCreator
    {
        // Common log-related constants
        private static readonly string DefaultLogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ScriptScheduler", "Logs");
            
        // Constants for task scheduling
        private const int DefaultRunInterval = 15; // minutes
        private const int DefaultRestartCount = 3;
        private const int DefaultExecutionHours = 3;

        /// <summary>
        /// Creates a scheduled task that runs a script truly hidden using native C# techniques
        /// </summary>
        public static bool CreateTrulyHiddenScriptTask(
            string taskName,
            string scriptPath,
            string scriptType,
            string workingDir,
            string args = "",
            string logDir = "",
            string userId = "",
            string password = "",
            bool highestPrivileges = true,
            string triggerType = "ONCE",
            DateTime? startTime = null,
            Dictionary<string, object> triggerParams = null)
        {
            try
            {
                // Set default values
                if (startTime == null)
                    startTime = DateTime.Now.AddMinutes(5);
                    
                if (string.IsNullOrEmpty(userId))
                    userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
                    
                if (string.IsNullOrEmpty(workingDir) && !string.IsNullOrEmpty(scriptPath))
                    workingDir = Path.GetDirectoryName(scriptPath);
                    
                if (string.IsNullOrEmpty(logDir))
                    logDir = DefaultLogDir;
                
                // Create log directory if it doesn't exist
                EnsureDirectoryExists(logDir);

                // Get assembly location for script wrapper location
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyLocation);
                
                // Create VBS wrapper directory
                string vbsWrapperDir = Path.Combine(Path.GetDirectoryName(assemblyLocation), "VBS Wrapper Scripts");
                if (!Directory.Exists(vbsWrapperDir))
                    Directory.CreateDirectory(vbsWrapperDir);
                
                // Use TaskWrapper to create the VBS wrapper script
                string vbsPath = TaskWrapper.CreateVBSWrapper(
                    taskName,
                    scriptPath,
                    scriptType,
                    vbsWrapperDir,
                    workingDir,
                    args,
                    logDir);

                using (TaskService ts = new TaskService())
                {
                    // Create a new task definition
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = $"Truly hidden execution of {scriptPath}";
                    
                    // Set principal (run level and user account)
                    td.Principal.RunLevel = highestPrivileges ? TaskRunLevel.Highest : TaskRunLevel.LUA;
                    td.Principal.UserId = userId;
                    
                    if (userId.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                        userId.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) ||
                        userId.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase))
                    {
                        td.Principal.LogonType = TaskLogonType.ServiceAccount;
                    }
                    else if (!string.IsNullOrEmpty(password))
                    {
                        td.Principal.LogonType = TaskLogonType.Password;
                    }
                    else
                    {
                        td.Principal.LogonType = TaskLogonType.InteractiveTokenOrPassword;
                    }
                    
                    // Add trigger
                    AddTrigger(td, triggerType, startTime.Value, triggerParams ?? new Dictionary<string, object>());
                    
                    // Create the action to run the VBS wrapper directly
                    // This is the key - we use wscript.exe which can run the script completely hidden
                    td.Actions.Add(new ExecAction("wscript.exe", $"\"{vbsPath}\"", workingDir));
                    
                    // Configure task settings
                    ConfigureTaskSettings(td, triggerParams ?? new Dictionary<string, object>());
                    
                    // Register the task
                    ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, 
                        userId, password, td.Principal.LogonType);
                    
                    Debug.WriteLine($"Truly hidden task '{taskName}' created successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating hidden task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a scheduled task for script execution - this method uses native C# techniques for truly hidden execution
        /// </summary>
        public static bool CreateTask(
            string taskName,
            string scriptPath,
            string scriptArgs = "",
            string workingDir = "",
            string triggerType = "ONCE",
            DateTime? startTime = null,
            bool useWrapper = false,
            string wrapperDir = "",
            string logDir = "",
            string userId = "",
            string password = "",
            bool highestPrivileges = true,
            Dictionary<string, object> triggerParams = null)
        {
            try
            {
                // Validate parameters and set defaults
                if (string.IsNullOrEmpty(taskName))
                    throw new ArgumentException("Task name is required");
                    
                if (string.IsNullOrEmpty(scriptPath))
                    throw new ArgumentException("Script path is required");
                
                // Set default values
                if (startTime == null)
                    startTime = DateTime.Now.AddMinutes(5);
                    
                if (string.IsNullOrEmpty(userId))
                    userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
                    
                if (string.IsNullOrEmpty(workingDir) && !string.IsNullOrEmpty(scriptPath))
                    workingDir = Path.GetDirectoryName(scriptPath);
                    
                if (string.IsNullOrEmpty(logDir))
                    logDir = DefaultLogDir;
                    
                if (string.IsNullOrEmpty(wrapperDir))
                    wrapperDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "ScriptScheduler", "Wrappers");
                        
                if (triggerParams == null)
                    triggerParams = new Dictionary<string, object>();
                
                // Create directories if needed
                EnsureDirectoryExists(workingDir);
                EnsureDirectoryExists(logDir);
                
                // Determine script type from extension
                string extension = Path.GetExtension(scriptPath).ToLowerInvariant();
                string scriptType = ScriptManager.Instance.GetScriptTypeFromExtension(extension);
                
                // If wrapper requested, create it
                string executablePath = scriptPath;
                string executableArgs = scriptArgs;
                
                // Use true native hidden execution (preferred method)
                return CreateTrulyHiddenScriptTask(
                    taskName,
                    scriptPath,
                    scriptType,
                    workingDir,
                    scriptArgs,
                    logDir,
                    userId,
                    password,
                    highestPrivileges,
                    triggerType,
                    startTime,
                    triggerParams);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registers a task with the Windows Task Scheduler
        /// </summary>
        private static bool RegisterScheduledTask(
            string taskName,
            string executablePath,
            string scriptType,
            string arguments,
            string workingDir,
            string triggerType,
            DateTime startTime,
            string userId,
            string password,
            bool highestPrivileges,
            Dictionary<string, object> triggerParams)
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    // Create task definition
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = $"Task created by ScriptScheduler on {DateTime.Now}";
                    td.Principal.RunLevel = highestPrivileges ? TaskRunLevel.Highest : TaskRunLevel.LUA;

                    // Set principal (user account)
                    td.Principal.UserId = userId;
                    if (userId.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                        userId.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) ||
                        userId.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase))
                    {
                        td.Principal.LogonType = TaskLogonType.ServiceAccount;
                    }
                    else if (!string.IsNullOrEmpty(password))
                    {
                        td.Principal.LogonType = TaskLogonType.Password;
                    }
                    else
                    {
                        td.Principal.LogonType = TaskLogonType.InteractiveTokenOrPassword;
                    }

                    // Create trigger based on trigger type
                    AddTrigger(td, triggerType, startTime, triggerParams);

                    // Configure action based on executable path and get hidden command settings
                    string actionPath;
                    string actionArgs;
                    
                    if (Path.GetExtension(executablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        // Direct executable
                        actionPath = executablePath;
                        actionArgs = arguments;
                    }
                    else
                    {
                        // Get appropriate command and arguments for hidden execution
                        var hiddenCommand = TaskWrapper.GetHiddenExecutionCommand(executablePath, scriptType, arguments);
                        actionPath = hiddenCommand.executable;
                        actionArgs = hiddenCommand.arguments;
                    }

                    // Add the action
                    td.Actions.Add(new ExecAction(actionPath, actionArgs, workingDir));

                    // Set additional settings
                    ConfigureTaskSettings(td, triggerParams);

                    // Register the task
                    ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, userId, password,
                        td.Principal.LogonType);

                    Debug.WriteLine($"Scheduled task '{taskName}' created successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering scheduled task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds the appropriate trigger to the task definition
        /// </summary>
        private static void AddTrigger(
            TaskDefinition td, 
            string triggerType, 
            DateTime startTime, 
            Dictionary<string, object> triggerParams)
        {
            switch (triggerType)
            {
                case "ONCE":
                    var onceTrigger = new TimeTrigger
                    {
                        StartBoundary = startTime
                    };
                    td.Triggers.Add(onceTrigger);
                    break;
                    
                case "DAILY":
                    var dailyTrigger = new DailyTrigger
                    {
                        StartBoundary = startTime
                    };
                    
                    // Set interval if provided
                    if (triggerParams.TryGetValue("DailyInterval", out object dailyIntervalObj) &&
                        int.TryParse(dailyIntervalObj.ToString(), out int dailyInterval))
                    {
                        dailyTrigger.DaysInterval = (short)dailyInterval;
                    }
                    
                    td.Triggers.Add(dailyTrigger);
                    break;
                    
                case "WEEKLY":
                    var weeklyTrigger = new WeeklyTrigger
                    {
                        StartBoundary = startTime
                    };
                    
                    // Set interval if provided
                    if (triggerParams.TryGetValue("WeeklyInterval", out object weeklyIntervalObj) &&
                        int.TryParse(weeklyIntervalObj.ToString(), out int weeklyInterval))
                    {
                        weeklyTrigger.WeeksInterval = (short)weeklyInterval;
                    }
                    
                    // Set days of week
                    if (triggerParams.TryGetValue("DaysOfWeek", out object daysOfWeekObj) &&
                        daysOfWeekObj is DaysOfTheWeek daysOfWeek)
                    {
                        weeklyTrigger.DaysOfWeek = daysOfWeek == 0 ? DaysOfTheWeek.Monday : daysOfWeek;
                    }
                    else
                    {
                        // Default to Monday if not specified
                        weeklyTrigger.DaysOfWeek = DaysOfTheWeek.Monday;
                    }
                    
                    td.Triggers.Add(weeklyTrigger);
                    break;
                    
                case "STARTUP":
                    var startupTrigger = new BootTrigger();
                    
                    // Add delay if specified
                    if (triggerParams.TryGetValue("StartupDelay", out object startupDelayObj) &&
                        int.TryParse(startupDelayObj.ToString(), out int startupDelay))
                    {
                        startupTrigger.Delay = TimeSpan.FromMinutes(startupDelay);
                    }
                    
                    td.Triggers.Add(startupTrigger);
                    break;
                    
                case "LOGON":
                    var logonTrigger = new LogonTrigger();
                    
                    // Add delay if specified
                    if (triggerParams.TryGetValue("LogonDelay", out object logonDelayObj) &&
                        int.TryParse(logonDelayObj.ToString(), out int logonDelay))
                    {
                        logonTrigger.Delay = TimeSpan.FromMinutes(logonDelay);
                    }
                    
                    // Set specific user if specified
                    if (triggerParams.TryGetValue("LogonUser", out object logonUserObj) &&
                        logonUserObj is string logonUser && !string.IsNullOrEmpty(logonUser))
                    {
                        logonTrigger.UserId = logonUser;
                    }
                    
                    td.Triggers.Add(logonTrigger);
                    break;
                    
                default:
                    // Default to a one-time trigger
                    td.Triggers.Add(new TimeTrigger { StartBoundary = startTime });
                    break;
            }
        }

        /// <summary>
        /// Configure additional task settings
        /// </summary>
        private static void ConfigureTaskSettings(TaskDefinition td, Dictionary<string, object> parameters)
        {
            // Set basic settings
            td.Settings.Hidden = false;
            td.Settings.AllowDemandStart = true;
            td.Settings.Enabled = true;
            
            // Allow task to be run on demand
            if (parameters.TryGetValue("AllowDemand", out object allowDemandObj) &&
                bool.TryParse(allowDemandObj.ToString(), out bool allowDemand))
            {
                td.Settings.AllowDemandStart = allowDemand;
            }
            
            // Set restart settings
            int restartCount = DefaultRestartCount;
            int restartInterval = DefaultRunInterval;
            
            if (parameters.TryGetValue("RestartCount", out object restartCountObj) &&
                int.TryParse(restartCountObj.ToString(), out int parsedRestartCount))
            {
                restartCount = parsedRestartCount;
            }
            
            if (parameters.TryGetValue("RestartInterval", out object restartIntervalObj) &&
                int.TryParse(restartIntervalObj.ToString(), out int parsedRestartInterval))
            {
                restartInterval = parsedRestartInterval;
            }
            
            td.Settings.RestartCount = restartCount;
            td.Settings.RestartInterval = TimeSpan.FromMinutes(restartInterval);
            
            // Set execution time limit
            int executionHours = DefaultExecutionHours;
            
            if (parameters.TryGetValue("ExecutionHours", out object executionHoursObj))
            {
                if (executionHoursObj is int hours)
                {
                    executionHours = hours;
                }
                else if (executionHoursObj is string hoursStr && int.TryParse(hoursStr, out int parsedHours))
                {
                    executionHours = parsedHours;
                }
            }
            
            // Check if no time limit is set
            if (parameters.TryGetValue("NoTimeLimit", out object noTimeLimitObj) &&
                bool.TryParse(noTimeLimitObj.ToString(), out bool noTimeLimit) &&
                noTimeLimit)
            {
                td.Settings.ExecutionTimeLimit = TimeSpan.Zero; // No limit
            }
            else
            {
                td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(executionHours);
            }
            
            // Run if missed (StartBoundaryMissed)
            if (parameters.TryGetValue("RunIfMissed", out object runIfMissedObj) &&
                bool.TryParse(runIfMissedObj.ToString(), out bool runIfMissed) &&
                runIfMissed)
            {
                td.Settings.StartWhenAvailable = true;
            }
            
            // Multi-instance policy
            if (parameters.TryGetValue("InstancePolicy", out object instancePolicyObj) &&
                instancePolicyObj is string instancePolicyStr)
            {
                switch (instancePolicyStr.ToUpperInvariant())
                {
                    case "QUEUE":
                        td.Settings.MultipleInstances = TaskInstancesPolicy.Queue;
                        break;
                    case "PARALLEL":
                        td.Settings.MultipleInstances = TaskInstancesPolicy.Parallel;
                        break;
                    case "IGNORE":
                        td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
                        break;
                    case "STOP":
                        td.Settings.MultipleInstances = TaskInstancesPolicy.StopExisting;
                        break;
                    default:
                        td.Settings.MultipleInstances = TaskInstancesPolicy.Queue;
                        break;
                }
            }
        }

        /// <summary>
        /// Ensures a directory exists, creating it if necessary
        /// </summary>
        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}