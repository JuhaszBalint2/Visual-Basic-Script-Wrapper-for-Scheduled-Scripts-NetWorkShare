using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.TaskScheduler;

namespace ScriptSchedulerApp
{
    public class ScriptManager
    {
        // Singleton instance
        private static ScriptManager? _instance;
        public static ScriptManager Instance => _instance ??= new ScriptManager();

        // Private constructor for singleton pattern
        private ScriptManager()
        {
        }

        /// <summary>
        /// Gets information about scripts in the specified directory
        /// </summary>
        /// <param name="directoryPath">Directory to search for scripts</param>
        /// <param name="scriptTypeFilter">Type of scripts to filter for, or "All"</param>
        /// <param name="nameFilter">Optional name filter</param>
        /// <returns>List of ScriptItems</returns>
        public List<ScriptItem> GetScripts(string directoryPath, string scriptTypeFilter = "All", string nameFilter = "")
        {
            List<ScriptItem> scripts = new List<ScriptItem>();
            
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Debug.WriteLine($"Directory not found: {directoryPath}");
                    return scripts;
                }

                // Define extensions to look for based on filter
                var extensions = new List<string>();
                if (scriptTypeFilter == "All" || string.IsNullOrWhiteSpace(scriptTypeFilter))
                {
                    extensions.AddRange(new[] { ".ps1", ".py", ".bat", ".cmd", ".vbs", ".js", ".exe" });
                }
                else
                {
                    switch (scriptTypeFilter)
                    {
                        case "PowerShell": extensions.Add(".ps1"); break;
                        case "Python": extensions.Add(".py"); break;
                        case "Batch": extensions.AddRange(new[] { ".bat", ".cmd" }); break;
                        case "VBScript": extensions.Add(".vbs"); break;
                        case "JavaScript": extensions.Add(".js"); break;
                        case "Executable": extensions.Add(".exe"); break;
                    }
                }

                // Find files matching extensions
                var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                // Apply name filter if provided
                if (!string.IsNullOrWhiteSpace(nameFilter))
                {
                    files = files.Where(f => Path.GetFileName(f).Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
                }

                // Convert to ScriptItems
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var scriptType = GetScriptTypeFromExtension(fileInfo.Extension);
                    
                    var item = new ScriptItem
                    {
                        Name = Path.GetFileName(file),
                        Path = file,
                        Type = scriptType,
                        LastModified = fileInfo.LastWriteTime,
                        SizeKB = Math.Round(fileInfo.Length / 1024.0, 2),
                        Description = GetScriptDescription(file, scriptType)
                    };
                    
                    scripts.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting scripts: {ex.Message}");
            }
            
            return scripts;
        }

        /// <summary>
        /// Creates a VBS wrapper script for a script
        /// </summary>
        /// <param name="scriptPath">Path to the script</param>
        /// <param name="outputPath">Path for the wrapper executable</param>
        /// <param name="scriptArgs">Script arguments</param>
        /// <param name="workingDir">Working directory</param>
        /// <param name="logDir">Directory for logs</param>
        /// <returns>True if successful</returns>
        public bool CreateVBSWrapper(string scriptPath, string outputPath, string scriptArgs = "", string workingDir = "", string logDir = "")
        {
            try
            {
                // Determine script type from extension
                string extension = Path.GetExtension(scriptPath).ToLowerInvariant();
                string scriptType = GetScriptTypeFromExtension(extension);
                
                // Use the TaskWrapper to create a VBS wrapper script
                string taskName = Path.GetFileNameWithoutExtension(outputPath);
                string wrapperDir = Path.GetDirectoryName(outputPath);
                
                // If log directory is not specified, create one next to the wrapper
                if (string.IsNullOrEmpty(logDir))
                {
                    logDir = Path.Combine(wrapperDir, "logs");
                }
                
                // Create the wrapper
                string wrapperPath = TaskWrapper.CreateVBSWrapper(
                    taskName,
                    scriptPath,
                    scriptType,
                    wrapperDir,
                    workingDir,
                    scriptArgs,
                    logDir);
                
                // Verify the wrapper was created
                if (File.Exists(wrapperPath))
                {
                    // If an explicit output path was requested, ensure file is there
                    if (!string.IsNullOrEmpty(outputPath) && outputPath != wrapperPath)
                    {
                        File.Copy(wrapperPath, outputPath, true);
                    }
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating VBS wrapper: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a scheduled task using the Windows Task Scheduler API
        /// </summary>
        /// <param name="taskName">Name of the task</param>
        /// <param name="description">Task description</param>
        /// <param name="scriptPath">Path to the script or wrapper</param>
        /// <param name="scriptType">Type of script</param>
        /// <param name="scriptArgs">Arguments for the script</param>
        /// <param name="workingDir">Working directory</param>
        /// <param name="principal">User account to run task as</param>
        /// <param name="password">Password for the user account (if needed)</param>
        /// <param name="highestPrivileges">Whether to run with highest privileges</param>
        /// <param name="triggerType">Type of trigger</param>
        /// <param name="startTime">Start time for the task</param>
        /// <param name="otherTriggerParams">Additional trigger parameters</param>
        /// <returns>True if successful</returns>
        public bool CreateScheduledTask(
            string taskName,
            string description,
            string scriptPath,
            string scriptType,
            string scriptArgs,
            string workingDir,
            string principal,
            string password,
            bool highestPrivileges,
            string triggerType,
            DateTime startTime,
            Dictionary<string, object> otherTriggerParams)
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    // Create task definition
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = description;
                    td.Principal.RunLevel = highestPrivileges ? TaskRunLevel.Highest : TaskRunLevel.LUA;

                    // Set principal (user account)
                    td.Principal.UserId = principal;
                    if (principal.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                        principal.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) ||
                        principal.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase))
                    {
                        td.Principal.LogonType = TaskLogonType.ServiceAccount;
                    }
                    else if (!string.IsNullOrEmpty(password))
                    {
                        td.Principal.LogonType = TaskLogonType.Password;
                    }
                    else
                    {
                        td.Principal.LogonType = TaskLogonType.InteractiveToken;
                    }

                    // Create trigger based on trigger type
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
                            if (otherTriggerParams.TryGetValue("DailyInterval", out object dailyIntervalObj) &&
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
                            if (otherTriggerParams.TryGetValue("WeeklyInterval", out object weeklyIntervalObj) &&
                                int.TryParse(weeklyIntervalObj.ToString(), out int weeklyInterval))
                            {
                                weeklyTrigger.WeeksInterval = (short)weeklyInterval;
                            }
                            
                            if (otherTriggerParams.TryGetValue("DaysOfWeek", out object daysOfWeekObj) &&
                                daysOfWeekObj is DaysOfTheWeek daysOfWeek)
                            {
                                weeklyTrigger.DaysOfWeek = daysOfWeek == 0 ? DaysOfTheWeek.Monday : daysOfWeek;
                            }
                            td.Triggers.Add(weeklyTrigger);
                            break;
                            
                        case "MONTHLY":
                            // Check which type of monthly trigger to create
                            if (otherTriggerParams.TryGetValue("MonthlyTriggerType", out object monthlyTriggerTypeObj) &&
                                monthlyTriggerTypeObj is string monthlyTriggerType)
                            {
                                if (monthlyTriggerType == "DayOfMonth")
                                {
                                    // Create a monthly trigger based on day of month
                                    var monthlyTrigger = new MonthlyTrigger
                                    {
                                        StartBoundary = startTime
                                    };
                                    
                                    // Set the day of month
                                    if (otherTriggerParams.TryGetValue("MonthlyDay", out object monthlyDayObj) &&
                                        int.TryParse(monthlyDayObj.ToString(), out int monthlyDay))
                                    {
                                        monthlyTrigger.DaysOfMonth = new int[] { monthlyDay };
                                    }
                                    else
                                    {
                                        monthlyTrigger.DaysOfMonth = new int[] { 1 }; // Default to day 1
                                    }
                                    
                                    // Set the months of year
                                    if (otherTriggerParams.TryGetValue("MonthlyInterval", out object monthlyIntervalObj) &&
                                        int.TryParse(monthlyIntervalObj.ToString(), out int monthlyInterval))
                                    {
                                        // Convert the interval to appropriate months of year
                                        if (monthlyInterval == 1)
                                        {
                                            // Every month
                                            monthlyTrigger.MonthsOfYear = MonthsOfTheYear.AllMonths;
                                        }
                                        else if (monthlyInterval == 2)
                                        {
                                            // Every 2 months - alternating months starting with January
                                            monthlyTrigger.MonthsOfYear = MonthsOfTheYear.January | MonthsOfTheYear.March | 
                                                                          MonthsOfTheYear.May | MonthsOfTheYear.July | 
                                                                          MonthsOfTheYear.September | MonthsOfTheYear.November;
                                        }
                                        else if (monthlyInterval == 3)
                                        {
                                            // Every 3 months - quarterly
                                            monthlyTrigger.MonthsOfYear = MonthsOfTheYear.January | MonthsOfTheYear.April | 
                                                                          MonthsOfTheYear.July | MonthsOfTheYear.October;
                                        }
                                        else if (monthlyInterval == 6)
                                        {
                                            // Every 6 months - semi-annually
                                            monthlyTrigger.MonthsOfYear = MonthsOfTheYear.January | MonthsOfTheYear.July;
                                        }
                                        else if (monthlyInterval == 12)
                                        {
                                            // Once a year
                                            monthlyTrigger.MonthsOfYear = MonthsOfTheYear.January;
                                        }
                                        else
                                        {
                                            // Default to all months if interval is not standard
                                            monthlyTrigger.MonthsOfYear = MonthsOfTheYear.AllMonths;
                                        }
                                    }
                                    
                                    td.Triggers.Add(monthlyTrigger);
                                }
                                else // DayOfWeekInMonth
                                {
                                    // Create a monthly DOW trigger
                                    var monthlyDOWTrigger = new MonthlyDOWTrigger
                                    {
                                        StartBoundary = startTime
                                    };
                                    
                                    // Set the week of month
                                    if (otherTriggerParams.TryGetValue("MonthlyWeekNumber", out object weekNumObj) &&
                                        int.TryParse(weekNumObj.ToString(), out int weekNum))
                                    {
                                        // Convert the week number to WhichWeek enum
                                        WhichWeek whichWeek = WhichWeek.FirstWeek;
                                        switch (weekNum)
                                        {
                                            case 1:
                                                whichWeek = WhichWeek.FirstWeek;
                                                break;
                                            case 2:
                                                whichWeek = WhichWeek.SecondWeek;
                                                break;
                                            case 3:
                                                whichWeek = WhichWeek.ThirdWeek;
                                                break;
                                            case 4:
                                                whichWeek = WhichWeek.FourthWeek;
                                                break;
                                            case 5:
                                                whichWeek = WhichWeek.LastWeek;
                                                break;
                                            default:
                                                whichWeek = WhichWeek.FirstWeek;
                                                break;
                                        }
                                        monthlyDOWTrigger.WeeksOfMonth = whichWeek;
                                    }
                                    
                                    // Set the day of week
                                    if (otherTriggerParams.TryGetValue("MonthlyDayOfWeek", out object dayOfWeekObj) &&
                                        dayOfWeekObj is DaysOfTheWeek dayOfWeek)
                                    {
                                        monthlyDOWTrigger.DaysOfWeek = dayOfWeek;
                                    }
                                    else
                                    {
                                        monthlyDOWTrigger.DaysOfWeek = DaysOfTheWeek.Monday; // Default to Monday
                                    }
                                    
                                    // Set the months of year for DOW trigger
                                    if (otherTriggerParams.TryGetValue("MonthlyDOWInterval", out object dowIntervalObj) &&
                                        int.TryParse(dowIntervalObj.ToString(), out int dowInterval))
                                    {
                                        // Convert the interval to appropriate months of year
                                        if (dowInterval == 1)
                                        {
                                            // Every month
                                            monthlyDOWTrigger.MonthsOfYear = MonthsOfTheYear.AllMonths;
                                        }
                                        else if (dowInterval == 2)
                                        {
                                            // Every 2 months - alternating months starting with January
                                            monthlyDOWTrigger.MonthsOfYear = MonthsOfTheYear.January | MonthsOfTheYear.March | 
                                                                             MonthsOfTheYear.May | MonthsOfTheYear.July | 
                                                                             MonthsOfTheYear.September | MonthsOfTheYear.November;
                                        }
                                        else if (dowInterval == 3)
                                        {
                                            // Every 3 months - quarterly
                                            monthlyDOWTrigger.MonthsOfYear = MonthsOfTheYear.January | MonthsOfTheYear.April | 
                                                                             MonthsOfTheYear.July | MonthsOfTheYear.October;
                                        }
                                        else if (dowInterval == 6)
                                        {
                                            // Every 6 months - semi-annually
                                            monthlyDOWTrigger.MonthsOfYear = MonthsOfTheYear.January | MonthsOfTheYear.July;
                                        }
                                        else if (dowInterval == 12)
                                        {
                                            // Once a year
                                            monthlyDOWTrigger.MonthsOfYear = MonthsOfTheYear.January;
                                        }
                                        else
                                        {
                                            // Default to all months if interval is not standard
                                            monthlyDOWTrigger.MonthsOfYear = MonthsOfTheYear.AllMonths;
                                        }
                                    }
                                    
                                    td.Triggers.Add(monthlyDOWTrigger);
                                }
                            }
                            else
                            {
                                // Default to simple monthly trigger on the 1st day of every month
                                var defaultMonthlyTrigger = new MonthlyTrigger
                                {
                                    StartBoundary = startTime,
                                    DaysOfMonth = new int[] { 1 }
                                };
                                td.Triggers.Add(defaultMonthlyTrigger);
                            }
                            break;
                            
                        case "STARTUP":
                            var startupTrigger = new BootTrigger();
                            if (otherTriggerParams.TryGetValue("StartupDelay", out object startupDelayObj) &&
                                int.TryParse(startupDelayObj.ToString(), out int startupDelay))
                            {
                                startupTrigger.Delay = TimeSpan.FromMinutes(startupDelay);
                            }
                            td.Triggers.Add(startupTrigger);
                            break;
                            
                        case "LOGON":
                            var logonTrigger = new LogonTrigger();
                            if (otherTriggerParams.TryGetValue("LogonDelay", out object logonDelayObj) &&
                                int.TryParse(logonDelayObj.ToString(), out int logonDelay))
                            {
                                logonTrigger.Delay = TimeSpan.FromMinutes(logonDelay);
                            }
                            
                            if (otherTriggerParams.TryGetValue("LogonUser", out object logonUserObj) &&
                                logonUserObj is string logonUser && !string.IsNullOrEmpty(logonUser))
                            {
                                logonTrigger.UserId = logonUser;
                            }
                            td.Triggers.Add(logonTrigger);
                            break;
                    }

                    // Configure action based on script type
                    string actionPath;
                    string actionArgs;
                    
                    if (scriptPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        // Executable (including C# wrapper)
                        actionPath = scriptPath;
                        actionArgs = scriptArgs; // Args may be empty if using a wrapper that already includes them
                    }
                    else if (scriptPath.EndsWith(".vbs", StringComparison.OrdinalIgnoreCase))
                    {
                        // For VBS files, always use wscript.exe
                        actionPath = "wscript.exe";
                        actionArgs = $"\"{scriptPath}\" {scriptArgs}";
                    }
                    else
                    {
                        // Handle different script types
                        switch (scriptType)
                        {
                            case "PowerShell":
                                actionPath = "powershell.exe";
                                actionArgs = $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\" {scriptArgs}";
                                break;
                            case "Python":
                                actionPath = "pythonw.exe";
                                actionArgs = $"\"{scriptPath}\" {scriptArgs}";
                                break;
                            case "Batch":
                                actionPath = "cmd.exe";
                                actionArgs = $"/c \"\"{scriptPath}\" {scriptArgs}\"";
                                break;
                            case "VBScript":
                                actionPath = "wscript.exe"; // Always use wscript.exe for VBS files
                                actionArgs = $"\"{scriptPath}\" {scriptArgs}";
                                break;
                            case "JavaScript":
                                actionPath = "wscript.exe";
                                actionArgs = $"\"{scriptPath}\" {scriptArgs}";
                                break;
                            default:
                                actionPath = scriptPath;
                                actionArgs = scriptArgs;
                                break;
                        }
                    }

                    // Add the action
                    td.Actions.Add(new ExecAction(actionPath, actionArgs, workingDir));

                    // Set basic settings
                    td.Settings.Hidden = false;
                    td.Settings.AllowDemandStart = true;
                    td.Settings.Enabled = true;
                    td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(1); // Default 1 hour time limit

                    // Check for multiple actions
                    if (otherTriggerParams.TryGetValue("AdditionalActions", out object additionalActionsObj) && 
                        additionalActionsObj is Dictionary<string, object> additionalActions)
                    {
                        td.AddStandardActionsFromDictionary(additionalActions); // Renamed method call
                    }

                    // Apply all other settings using the extension method
                    TaskSchedulerExtensions.EnhanceTaskDefinition(td, otherTriggerParams); // Explicit call to resolve ambiguity
                    
                    // For backward compatibility, still use specific properties if they aren't handled by EnhanceTaskDefinition
                    // This will be removed later after full migration
                    if (!otherTriggerParams.ContainsKey("RestartCount") && !otherTriggerParams.ContainsKey("RestartInterval"))
                    {
                        // If restart count and interval are specified, configure that
                        if (otherTriggerParams.TryGetValue("RestartCount", out object restartCountObj) &&
                            int.TryParse(restartCountObj.ToString(), out int restartCount) &&
                            restartCount > 0 &&
                            otherTriggerParams.TryGetValue("RestartInterval", out object restartIntervalObj) &&
                            int.TryParse(restartIntervalObj.ToString(), out int restartInterval) &&
                            restartInterval > 0)
                        {
                            td.Settings.RestartCount = restartCount;
                            td.Settings.RestartInterval = TimeSpan.FromMinutes(restartInterval);
                        }
                    }
                    
                    if (!otherTriggerParams.ContainsKey("ExecutionTimeLimit"))
                    {
                        // Set execution time limit if specified
                        if (otherTriggerParams.TryGetValue("ExecutionHours", out object executionHoursObj) &&
                            int.TryParse(executionHoursObj.ToString(), out int executionHours) &&
                            executionHours > 0)
                        {
                            td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(executionHours);
                        }
                    }

                    // Register the task
                    ts.RootFolder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate, principal, password,
                        td.Principal.LogonType);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating scheduled task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to extract a description from a script file
        /// </summary>
        private string GetScriptDescription(string filePath, string scriptType)
        {
            try
            {
                if (!File.Exists(filePath))
                    return string.Empty;

                string[] firstLines = File.ReadLines(filePath).Take(10).ToArray();
                
                switch (scriptType)
                {
                    case "PowerShell":
                        // Look for comment block or first single comment
                        var commentBlock = firstLines.SkipWhile(l => !l.Trim().StartsWith("<#"))
                                                    .TakeWhile(l => !l.Trim().EndsWith("#>"))
                                                    .ToList();
                        if (commentBlock.Any())
                            return string.Join(" ", commentBlock.Select(l => l.Trim())).Trim();
                        
                        // Or check for # comments
                        var comment = firstLines.FirstOrDefault(l => l.Trim().StartsWith("#") && !l.Trim().StartsWith("#!"));
                        if (comment != null)
                            return comment.Trim().TrimStart('#').Trim();
                        break;
                        
                    case "Python":
                        // Look for docstring or # comments
                        var pythonDocString = firstLines.SkipWhile(l => !l.Trim().StartsWith("\"\"\"") && !l.Trim().StartsWith("'''"))
                                                       .TakeWhile(l => !l.Trim().EndsWith("\"\"\"") && !l.Trim().EndsWith("'''"))
                                                       .ToList();
                        if (pythonDocString.Any())
                            return string.Join(" ", pythonDocString.Select(l => l.Trim())).Trim();
                        
                        // Or check for # comments
                        var pythonComment = firstLines.FirstOrDefault(l => l.Trim().StartsWith("#") && !l.Trim().StartsWith("#!"));
                        if (pythonComment != null)
                            return pythonComment.Trim().TrimStart('#').Trim();
                        break;
                        
                    case "Batch":
                        // Look for REM or :: comments
                        var batchComment = firstLines.FirstOrDefault(l => l.Trim().StartsWith("REM ") || l.Trim().StartsWith("::"));
                        if (batchComment != null)
                            return batchComment.Trim().TrimStart('R', 'E', 'M', ':', ' ').Trim();
                        break;
                        
                    case "VBScript":
                    case "JavaScript":
                        // Look for ' or // comments
                        var scriptComment = firstLines.FirstOrDefault(l => l.Trim().StartsWith("'") || l.Trim().StartsWith("//"));
                        if (scriptComment != null)
                            return scriptComment.Trim().TrimStart('\'', '/', ' ').Trim();
                        break;
                }
                
                // Default: return filename as description
                return Path.GetFileName(filePath);
            }
            catch
            {
                // In case of any error, just return the filename
                return Path.GetFileName(filePath);
            }
        }

        /// <summary>
        /// Determine script type from file extension
        /// </summary>
        public string GetScriptTypeFromExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "Unknown";
                
            extension = extension.ToLowerInvariant();
            
            switch (extension)
            {
                case ".ps1": return "PowerShell";
                case ".py": return "Python";
                case ".bat":
                case ".cmd": return "Batch";
                case ".vbs": return "VBScript";
                case ".js": return "JavaScript";
                case ".exe": return "Executable";
                default: return "Unknown";
            }
        }
    }
}