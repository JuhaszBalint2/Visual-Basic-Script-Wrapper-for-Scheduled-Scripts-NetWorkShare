using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32.TaskScheduler;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Command-line application entry point that provides command-line functionality
    /// to replace PowerShell and VBS scripts
    /// </summary>
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // If no arguments, or with --gui or -g flag, launch the GUI application
            if (args.Length == 0 || args.Contains("--gui") || args.Contains("-g"))
            {
                // Launch the WPF application
                var app = new App();
                app.InitializeComponent();
                app.Run();
                return;
            }

            // Special case for direct run command
            if (args.Length >= 2 && args[0].Equals("/run", StringComparison.OrdinalIgnoreCase))
            {
                RunScriptHiddenDirect(args);
                return;
            }

            // Command line mode - parse arguments
            if (TryParseCommandLine(args, out Dictionary<string, string> options))
            {
                // Process commands
                string command = options.ContainsKey("command") ? options["command"] : "help";
                
                switch (command.ToLowerInvariant())
                {
                    case "wrapper":
                    case "create-wrapper":
                        CreateWrapper(options);
                        break;
                        
                    case "task":
                    case "create-task":
                        CreateTask(options);
                        break;
                    
                    case "run":
                    case "run-hidden":
                        RunScriptHidden(options);
                        break;
                        
                    case "help":
                    default:
                        ShowHelp();
                        break;
                }
            }
            else
            {
                Console.WriteLine("Error parsing command line arguments.\n");
                ShowHelp();
            }
        }

        /// <summary>
        /// Parse command line arguments into a dictionary of options
        /// </summary>
        private static bool TryParseCommandLine(string[] args, out Dictionary<string, string> options)
        {
            options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // First argument is the command
                if (args.Length > 0)
                {
                    options["command"] = args[0];
                }
                
                // Process remaining arguments
                for (int i = 1; i < args.Length; i++)
                {
                    string arg = args[i];
                    
                    // Handle --option=value format
                    if (arg.StartsWith("--") && arg.Contains('='))
                    {
                        int equalPos = arg.IndexOf('=');
                        string key = arg.Substring(2, equalPos - 2);
                        string value = arg.Substring(equalPos + 1);
                        options[key] = value;
                        continue;
                    }
                    
                    // Handle --option value format
                    if (arg.StartsWith("--"))
                    {
                        string key = arg.Substring(2);
                        
                        // Check if next argument is a value (not another option)
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            options[key] = args[i + 1];
                            i++; // Skip the value in next iteration
                        }
                        else
                        {
                            // Flag-style option with no value
                            options[key] = "true";
                        }
                        continue;
                    }
                    
                    // Handle -o value format (short options)
                    if (arg.StartsWith("-") && arg.Length == 2)
                    {
                        string key = GetLongOptionName(arg[1]);
                        
                        // Check if next argument is a value
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            options[key] = args[i + 1];
                            i++; // Skip the value in next iteration
                        }
                        else
                        {
                            // Flag-style option
                            options[key] = "true";
                        }
                        continue;
                    }
                    
                    // Handle positional arguments if needed
                    // ...
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing arguments: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the long option name from a short option character
        /// </summary>
        private static string GetLongOptionName(char shortOption)
        {
            switch (shortOption)
            {
                case 'n': return "name";
                case 's': return "script";
                case 'a': return "args";
                case 'w': return "workdir";
                case 'o': return "output";
                case 't': return "type";
                case 'l': return "logdir";
                case 'u': return "user";
                case 'p': return "password";
                case 'h': return "help";
                default: return shortOption.ToString();
            }
        }

        /// <summary>
        /// Run a script in completely hidden mode - direct arguments version
        /// </summary>
        private static void RunScriptHiddenDirect(string[] args)
        {
            try
            {
                // Command format: /run "scriptPath" "scriptType" "workingDir" "logDir" "args"
                if (args.Length < 6)
                {
                    Console.WriteLine("Error: Insufficient arguments for /run command");
                    Environment.Exit(1);
                    return;
                }

                string scriptPath = args[1];
                string scriptType = args[2];
                string workingDir = args[3];
                string logDir = args[4];
                string scriptArgs = args[5];
                
                // Run the script hidden using the HiddenScriptRunner
                HiddenScriptRunner.RunScriptHidden(scriptPath, scriptType, scriptArgs, workingDir, logDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running script hidden: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Run a script in completely hidden mode - options dictionary version
        /// </summary>
        private static void RunScriptHidden(Dictionary<string, string> options)
        {
            try
            {
                // Required parameters
                if (!options.TryGetValue("script", out string scriptPath) && !options.TryGetValue("s", out scriptPath))
                {
                    Console.WriteLine("Error: Script path is required. Use --script=path or -s path");
                    return;
                }
                
                // Get script type
                string scriptType;
                if (!options.TryGetValue("type", out scriptType) && !options.TryGetValue("t", out scriptType))
                {
                    scriptType = ScriptManager.Instance.GetScriptTypeFromExtension(Path.GetExtension(scriptPath));
                }
                
                // Optional parameters with defaults
                if (!options.TryGetValue("args", out string scriptArgs) && !options.TryGetValue("a", out scriptArgs))
                {
                    scriptArgs = "";
                }
                
                if (!options.TryGetValue("workdir", out string workingDir) && !options.TryGetValue("w", out workingDir))
                {
                    workingDir = Path.GetDirectoryName(scriptPath);
                }
                
                if (!options.TryGetValue("logdir", out string logDir) && !options.TryGetValue("l", out logDir))
                {
                    logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "ScriptScheduler", "Logs");
                }
                
                // Run the script hidden using the HiddenScriptRunner
                HiddenScriptRunner.RunScriptHidden(scriptPath, scriptType, scriptArgs, workingDir, logDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running script hidden: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Create a C# wrapper for a script
        /// </summary>
        private static void CreateWrapper(Dictionary<string, string> options)
        {
            try
            {
                // Required parameters
                if (!options.TryGetValue("script", out string scriptPath) && !options.TryGetValue("s", out scriptPath))
                {
                    Console.WriteLine("Error: Script path is required. Use --script=path or -s path");
                    return;
                }
                
                // Optional parameters with defaults
                if (!options.TryGetValue("output", out string outputPath) && !options.TryGetValue("o", out outputPath))
                {
                    // Default output path is same directory with .exe extension
                    outputPath = Path.ChangeExtension(scriptPath, ".exe");
                }
                
                if (!options.TryGetValue("args", out string scriptArgs) && !options.TryGetValue("a", out scriptArgs))
                {
                    scriptArgs = "";
                }
                
                if (!options.TryGetValue("workdir", out string workingDir) && !options.TryGetValue("w", out workingDir))
                {
                    workingDir = Path.GetDirectoryName(scriptPath);
                }
                
                if (!options.TryGetValue("logdir", out string logDir) && !options.TryGetValue("l", out logDir))
                {
                    logDir = Path.Combine(Path.GetDirectoryName(outputPath), "logs");
                }
                
                // Create task name from output filename
                string taskName = Path.GetFileNameWithoutExtension(outputPath);
                
                // Determine script type
                string scriptType = ScriptManager.Instance.GetScriptTypeFromExtension(Path.GetExtension(scriptPath));
                
                Console.WriteLine($"Creating VBS wrapper script for {scriptType} script: {scriptPath}");
                Console.WriteLine($"Output: {outputPath}");
                
                // Create the wrapper
                string wrapperPath = TaskWrapper.CreateVBSWrapper(
                    taskName,
                    scriptPath,
                    scriptType,
                    Path.GetDirectoryName(outputPath),
                    workingDir,
                    scriptArgs,
                    logDir);
                
                // If output path is different from what TaskWrapper created, copy the file
                if (!string.IsNullOrEmpty(wrapperPath) && wrapperPath != outputPath && File.Exists(wrapperPath))
                {
                    File.Copy(wrapperPath, outputPath, true);
                    Console.WriteLine($"Wrapper created successfully at: {outputPath}");
                }
                else if (!string.IsNullOrEmpty(wrapperPath) && File.Exists(wrapperPath))
                {
                    Console.WriteLine($"Wrapper created successfully at: {wrapperPath}");
                }
                else
                {
                    Console.WriteLine("Failed to create wrapper.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating wrapper: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Create a scheduled task
        /// </summary>
        private static void CreateTask(Dictionary<string, string> options)
        {
            try
            {
                // Required parameters
                if (!options.TryGetValue("name", out string taskName) && !options.TryGetValue("n", out taskName))
                {
                    Console.WriteLine("Error: Task name is required. Use --name=name or -n name");
                    return;
                }
                
                if (!options.TryGetValue("script", out string scriptPath) && !options.TryGetValue("s", out scriptPath))
                {
                    Console.WriteLine("Error: Script path is required. Use --script=path or -s path");
                    return;
                }
                
                // Optional parameters with defaults
                if (!options.TryGetValue("args", out string scriptArgs) && !options.TryGetValue("a", out scriptArgs))
                {
                    scriptArgs = "";
                }
                
                if (!options.TryGetValue("workdir", out string workingDir) && !options.TryGetValue("w", out workingDir))
                {
                    workingDir = Path.GetDirectoryName(scriptPath);
                }
                
                // Get trigger type
                if (!options.TryGetValue("trigger", out string triggerType))
                {
                    triggerType = "ONCE"; // Default
                }
                
                // Get start time
                DateTime startTime = DateTime.Now.AddMinutes(5); // Default 5 minutes from now
                if (options.TryGetValue("starttime", out string startTimeStr))
                {
                    if (DateTime.TryParse(startTimeStr, out DateTime parsedTime))
                    {
                        startTime = parsedTime;
                    }
                }
                
                // Get whether to use wrapper
                bool useWrapper = false;
                if (options.TryGetValue("wrapper", out string wrapperStr))
                {
                    bool.TryParse(wrapperStr, out useWrapper);
                }
                
                // Get wrapper directory
                if (!options.TryGetValue("wrapperdir", out string wrapperDir))
                {
                    wrapperDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "ScriptScheduler", "Wrappers");
                }
                
                // Get log directory
                if (!options.TryGetValue("logdir", out string logDir) && !options.TryGetValue("l", out logDir))
                {
                    logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "ScriptScheduler", "Logs");
                }
                
                // Get user id and password
                if (!options.TryGetValue("user", out string userId) && !options.TryGetValue("u", out userId))
                {
                    userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
                }
                
                if (!options.TryGetValue("password", out string password) && !options.TryGetValue("p", out password))
                {
                    password = "";
                }
                
                // Get highest privileges flag
                bool highestPrivileges = true; // Default
                if (options.TryGetValue("highest", out string highestStr))
                {
                    bool.TryParse(highestStr, out highestPrivileges);
                }
                
                // Additional trigger parameters
                var triggerParams = new Dictionary<string, object>();
                
                // Parse trigger-specific parameters
                ParseTriggerParameters(options, triggerType, triggerParams);
                
                Console.WriteLine($"Creating scheduled task: {taskName}");
                Console.WriteLine($"Script: {scriptPath}");
                Console.WriteLine($"Trigger type: {triggerType}");
                
                // Create the task
                bool success = TaskCreator.CreateTask(
                    taskName,
                    scriptPath,
                    scriptArgs,
                    workingDir,
                    triggerType,
                    startTime,
                    useWrapper,
                    wrapperDir,
                    logDir,
                    userId,
                    password,
                    highestPrivileges,
                    triggerParams);
                
                if (success)
                {
                    Console.WriteLine("Task created successfully!");
                }
                else
                {
                    Console.WriteLine("Failed to create task.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Parse trigger-specific parameters from command line options
        /// </summary>
        private static void ParseTriggerParameters(
            Dictionary<string, string> options, 
            string triggerType, 
            Dictionary<string, object> triggerParams)
        {
            switch (triggerType.ToUpperInvariant())
            {
                case "DAILY":
                    if (options.TryGetValue("interval", out string dailyInterval) && 
                        int.TryParse(dailyInterval, out int dailyIntervalValue))
                    {
                        triggerParams["DailyInterval"] = dailyIntervalValue;
                    }
                    break;
                    
                case "WEEKLY":
                    if (options.TryGetValue("interval", out string weeklyInterval) && 
                        int.TryParse(weeklyInterval, out int weeklyIntervalValue))
                    {
                        triggerParams["WeeklyInterval"] = weeklyIntervalValue;
                    }
                    
                    if (options.TryGetValue("days", out string daysOfWeekStr))
                    {
                        DaysOfTheWeek daysOfWeek = DaysOfTheWeek.Monday; // Default
                        foreach (string day in daysOfWeekStr.Split(','))
                        {
                            switch (day.Trim().ToUpperInvariant())
                            {
                                case "MON":
                                case "MONDAY":
                                    daysOfWeek |= DaysOfTheWeek.Monday;
                                    break;
                                case "TUE":
                                case "TUESDAY":
                                    daysOfWeek |= DaysOfTheWeek.Tuesday;
                                    break;
                                case "WED":
                                case "WEDNESDAY":
                                    daysOfWeek |= DaysOfTheWeek.Wednesday;
                                    break;
                                case "THU":
                                case "THURSDAY":
                                    daysOfWeek |= DaysOfTheWeek.Thursday;
                                    break;
                                case "FRI":
                                case "FRIDAY":
                                    daysOfWeek |= DaysOfTheWeek.Friday;
                                    break;
                                case "SAT":
                                case "SATURDAY":
                                    daysOfWeek |= DaysOfTheWeek.Saturday;
                                    break;
                                case "SUN":
                                case "SUNDAY":
                                    daysOfWeek |= DaysOfTheWeek.Sunday;
                                    break;
                            }
                        }
                        triggerParams["DaysOfWeek"] = daysOfWeek;
                    }
                    break;
                    
                case "STARTUP":
                    if (options.TryGetValue("delay", out string startupDelay) && 
                        int.TryParse(startupDelay, out int startupDelayValue))
                    {
                        triggerParams["StartupDelay"] = startupDelayValue;
                    }
                    break;
                    
                case "LOGON":
                    if (options.TryGetValue("delay", out string logonDelay) && 
                        int.TryParse(logonDelay, out int logonDelayValue))
                    {
                        triggerParams["LogonDelay"] = logonDelayValue;
                    }
                    
                    if (options.TryGetValue("logonuser", out string logonUser))
                    {
                        triggerParams["LogonUser"] = logonUser;
                    }
                    break;
            }
            
            // Common parameters
            if (options.TryGetValue("restart", out string restartStr) && 
                restartStr.Contains(','))
            {
                string[] restartParts = restartStr.Split(',');
                if (restartParts.Length == 2 && 
                    int.TryParse(restartParts[0], out int count) && 
                    int.TryParse(restartParts[1], out int interval))
                {
                    triggerParams["RestartCount"] = count;
                    triggerParams["RestartInterval"] = interval;
                }
            }
            else
            {
                // Individual parameters
                if (options.TryGetValue("restart-count", out string restartCount) && 
                    int.TryParse(restartCount, out int countValue))
                {
                    triggerParams["RestartCount"] = countValue;
                }
                
                if (options.TryGetValue("restart-interval", out string restartInterval) && 
                    int.TryParse(restartInterval, out int intervalValue))
                {
                    triggerParams["RestartInterval"] = intervalValue;
                }
            }
            
            // Execution time limit
            if (options.TryGetValue("execution-hours", out string executionHours) && 
                int.TryParse(executionHours, out int hoursValue))
            {
                triggerParams["ExecutionHours"] = hoursValue;
            }
            
            // No time limit flag
            if (options.TryGetValue("no-time-limit", out string noTimeLimitStr))
            {
                bool noTimeLimit = false;
                if (bool.TryParse(noTimeLimitStr, out bool parsed))
                {
                    noTimeLimit = parsed;
                }
                else if (noTimeLimitStr.ToLowerInvariant() == "true" || noTimeLimitStr == "1")
                {
                    noTimeLimit = true;
                }
                
                if (noTimeLimit)
                {
                    triggerParams["NoTimeLimit"] = true;
                }
            }
            
            // Run if missed flag
            if (options.TryGetValue("run-if-missed", out string runIfMissedStr))
            {
                bool runIfMissed = false;
                if (bool.TryParse(runIfMissedStr, out bool parsed))
                {
                    runIfMissed = parsed;
                }
                else if (runIfMissedStr.ToLowerInvariant() == "true" || runIfMissedStr == "1")
                {
                    runIfMissed = true;
                }
                
                if (runIfMissed)
                {
                    triggerParams["RunIfMissed"] = true;
                }
            }
            
            // Instance policy
            if (options.TryGetValue("instance-policy", out string instancePolicy))
            {
                triggerParams["InstancePolicy"] = instancePolicy.ToUpperInvariant();
            }
        }

        /// <summary>
        /// Show help message
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("Script Scheduler - Task Wrapper and Scheduler");
            Console.WriteLine("===========================================");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  create-wrapper: Create a VBS wrapper script for a script");
            Console.WriteLine("  create-task: Create a scheduled task");
            Console.WriteLine("  run-hidden: Run a script completely hidden");
            Console.WriteLine("  help: Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  ScriptSchedulerApp.exe create-wrapper --script=C:\\Scripts\\MyScript.ps1 --output=C:\\Wrappers\\MyScript.exe");
            Console.WriteLine("  ScriptSchedulerApp.exe create-task --name=\"My Task\" --script=C:\\Scripts\\MyScript.ps1 --trigger=DAILY --interval=1");
            Console.WriteLine("  ScriptSchedulerApp.exe run-hidden --script=C:\\Scripts\\MyScript.ps1 --args=\"-Param1 Value1\"");
            Console.WriteLine();
            Console.WriteLine("Options for create-wrapper:");
            Console.WriteLine("  --script, -s: Path to the script to wrap (required)");
            Console.WriteLine("  --output, -o: Path for the output wrapper executable");
            Console.WriteLine("  --args, -a: Arguments for the script");
            Console.WriteLine("  --workdir, -w: Working directory for script execution");
            Console.WriteLine("  --logdir, -l: Directory for log files");
            Console.WriteLine();
            Console.WriteLine("Options for create-task:");
            Console.WriteLine("  --name, -n: Name for the task (required)");
            Console.WriteLine("  --script, -s: Path to the script (required)");
            Console.WriteLine("  --args, -a: Arguments for the script");
            Console.WriteLine("  --workdir, -w: Working directory for script execution");
            Console.WriteLine("  --trigger: Trigger type (ONCE, DAILY, WEEKLY, STARTUP, LOGON)");
            Console.WriteLine("  --starttime: Start time for the task (default: 5 minutes from now)");
            Console.WriteLine("  --wrapper: Whether to create a VBS wrapper script (true/false)");
            Console.WriteLine("  --wrapperdir: Directory for the wrapper executable");
            Console.WriteLine("  --logdir, -l: Directory for log files");
            Console.WriteLine("  --user, -u: User account to run the task as");
            Console.WriteLine("  --password, -p: Password for the user account");
            Console.WriteLine("  --highest: Run with highest privileges (true/false)");
            Console.WriteLine();
            Console.WriteLine("Options for run-hidden:");
            Console.WriteLine("  --script, -s: Path to the script to run (required)");
            Console.WriteLine("  --type, -t: Script type (PowerShell, Python, Batch, etc.)");
            Console.WriteLine("  --args, -a: Arguments for the script");
            Console.WriteLine("  --workdir, -w: Working directory for script execution");
            Console.WriteLine("  --logdir, -l: Directory for log files");
            Console.WriteLine();
            Console.WriteLine("Trigger-specific options:");
            Console.WriteLine("  --interval: Interval for DAILY or WEEKLY triggers");
            Console.WriteLine("  --days: Days of week for WEEKLY trigger (MON,TUE,WED,...)");
            Console.WriteLine("  --delay: Delay in minutes for STARTUP or LOGON triggers");
            Console.WriteLine("  --logonuser: Specific user for LOGON trigger");
            Console.WriteLine();
            Console.WriteLine("Common options:");
            Console.WriteLine("  --restart: Restart count and interval (count,interval)");
            Console.WriteLine("  --restart-count: Number of times to restart if failed");
            Console.WriteLine("  --restart-interval: Interval in minutes between restarts");
            Console.WriteLine("  --execution-hours: Time limit in hours for task execution");
            Console.WriteLine("  --no-time-limit: No time limit for execution (true/false)");
            Console.WriteLine("  --run-if-missed: Run if task is missed (true/false)");
            Console.WriteLine("  --instance-policy: How to handle multiple instances (QUEUE, PARALLEL, IGNORE, STOP)");
        }
    }
}