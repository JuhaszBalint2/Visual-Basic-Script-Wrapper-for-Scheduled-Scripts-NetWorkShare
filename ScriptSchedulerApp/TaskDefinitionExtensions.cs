using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32.TaskScheduler;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Extension methods for TaskDefinition to enhance functionality
    /// </summary>
    public static class TaskDefinitionExtensions
    {
        /// <summary>
        /// Enhances a task definition with additional parameters
        /// </summary>
        /// <param name="td">The task definition to enhance</param>
        /// <param name="parameters">Dictionary of parameters to apply</param>
        public static void ApplySpecificSettings(this TaskDefinition td, Dictionary<string, object> parameters) // Renamed method to avoid ambiguity
        {
            if (td == null || parameters == null)
                return;
            
            try
            {
                // Allow demand start
                if (parameters.TryGetValue("AllowDemandStart", out object allowDemandObj) &&
                    allowDemandObj is bool allowDemand)
                {
                    td.Settings.AllowDemandStart = allowDemand;
                }
                
                // Run task if missed
                if (parameters.TryGetValue("RunIfMissed", out object runIfMissedObj) &&
                    runIfMissedObj is bool runIfMissed)
                {
                    td.Settings.StartWhenAvailable = runIfMissed;
                }
                
                // Restart on failure
                if (parameters.TryGetValue("RestartOnFailure", out object restartOnFailureObj) &&
                    restartOnFailureObj is bool restartOnFailure && restartOnFailure)
                {
                    int restartCount = 3; // Default
                    if (parameters.TryGetValue("RestartCount", out object restartCountObj) &&
                        int.TryParse(restartCountObj.ToString(), out int parsedCount))
                    {
                        restartCount = parsedCount;
                    }
                    
                    int restartInterval = 15; // Default in minutes
                    if (parameters.TryGetValue("RestartInterval", out object restartIntervalObj) &&
                        int.TryParse(restartIntervalObj.ToString(), out int parsedInterval))
                    {
                        restartInterval = parsedInterval;
                    }
                    
                    td.Settings.RestartCount = restartCount;
                    td.Settings.RestartInterval = TimeSpan.FromMinutes(restartInterval);
                }
                
                // Execution time limit
                if (parameters.TryGetValue("ExecutionTimeLimit", out object timeLimitObj))
                {
                    if (int.TryParse(timeLimitObj.ToString(), out int timeLimit))
                    {
                        if (timeLimit <= 0)
                        {
                            // No time limit
                            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                        }
                        else
                        {
                            // Convert hours to timespan
                            td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(timeLimit);
                        }
                    }
                }
                
                // Multiple instances policy
                if (parameters.TryGetValue("MultipleInstances", out object multipleInstancesObj) &&
                    int.TryParse(multipleInstancesObj.ToString(), out int multipleInstancesValue))
                {
                    // Convert int value to TaskInstancesPolicy enum
                    switch (multipleInstancesValue)
                    {
                        case 0:
                            td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
                            break;
                        case 1:
                            td.Settings.MultipleInstances = TaskInstancesPolicy.Parallel;
                            break;
                        case 2:
                            td.Settings.MultipleInstances = TaskInstancesPolicy.Queue;
                            break;
                        case 3:
                            td.Settings.MultipleInstances = TaskInstancesPolicy.StopExisting;
                            break;
                    }
                }
                
                // Network condition settings
                if (parameters.TryGetValue("RunOnlyIfNetworkAvailable", out object networkConditionObj) &&
                    networkConditionObj is bool networkCondition)
                {
                    td.Settings.RunOnlyIfNetworkAvailable = networkCondition;
                }
                
                // Battery settings
                if (parameters.TryGetValue("StopIfGoingOnBatteries", out object stopOnBatteryObj) &&
                    stopOnBatteryObj is bool stopOnBattery)
                {
                    td.Settings.StopIfGoingOnBatteries = stopOnBattery;
                }
                
                if (parameters.TryGetValue("DisallowStartIfOnBatteries", out object disallowStartOnBatteryObj) &&
                    disallowStartOnBatteryObj is bool disallowStartOnBattery)
                {
                    td.Settings.DisallowStartIfOnBatteries = disallowStartOnBattery;
                }
                
                // Wake settings
                if (parameters.TryGetValue("WakeToRun", out object wakeToRunObj) &&
                    wakeToRunObj is bool wakeToRun)
                {
                    td.Settings.WakeToRun = wakeToRun;
                }
                
                // Idle settings
                if (parameters.TryGetValue("RunOnlyIfIdle", out object runOnlyIfIdleObj) &&
                    runOnlyIfIdleObj is bool runOnlyIfIdle && runOnlyIfIdle)
                {
                    td.Settings.RunOnlyIfIdle = true;
                    
                    if (parameters.TryGetValue("IdleMinutes", out object idleMinutesObj) &&
                        int.TryParse(idleMinutesObj.ToString(), out int idleMinutes))
                    {
                        td.Settings.IdleSettings.IdleDuration = TimeSpan.FromMinutes(idleMinutes);
                    }
                    
                    if (parameters.TryGetValue("IdleWaitTimeout", out object idleWaitTimeoutObj) &&
                        int.TryParse(idleWaitTimeoutObj.ToString(), out int idleWaitTimeout))
                    {
                        td.Settings.IdleSettings.WaitTimeout = TimeSpan.FromMinutes(idleWaitTimeout);
                    }
                    
                    if (parameters.TryGetValue("IdleStopOnEnd", out object idleStopOnEndObj) &&
                        idleStopOnEndObj is bool idleStopOnEnd)
                    {
                        td.Settings.IdleSettings.StopOnIdleEnd = idleStopOnEnd;
                    }
                    
                    if (parameters.TryGetValue("IdleRestartOnIdle", out object idleRestartOnIdleObj) &&
                        idleRestartOnIdleObj is bool idleRestartOnIdle)
                    {
                        td.Settings.IdleSettings.RestartOnIdle = idleRestartOnIdle;
                    }
                }
                
                // Force stop setting
                if (parameters.TryGetValue("ForceStop", out object forceStopObj) &&
                    forceStopObj is bool forceStop)
                {
                    // Kill task if it doesn't terminate normally
                    td.Settings.DeleteExpiredTaskAfter = TimeSpan.Zero;
                }
                
                // Delete when expired setting
                if (parameters.TryGetValue("DeleteWhenExpired", out object deleteWhenExpiredObj) &&
                    deleteWhenExpiredObj is bool deleteWhenExpired)
                {
                    if (deleteWhenExpired)
                    {
                        // Default to 30 days if not specified
                        td.Settings.DeleteExpiredTaskAfter = TimeSpan.FromDays(30);
                    }
                }
                
                // Add any additional actions
                if (parameters.TryGetValue("AdditionalActions", out object additionalActionsObj) &&
                    additionalActionsObj is Dictionary<string, object> additionalActions)
                {
                    td.AddDisplayMessageActionsFromList(additionalActions); // Renamed method call
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enhancing task definition: {ex.Message}");
                // Continue with base settings
            }
        }

        /// <summary>
        /// Adds multiple actions to a task definition
        /// </summary>
        /// <param name="td">The task definition</param>
        /// <param name="additionalActions">Dictionary containing additional actions</param>
        public static void AddDisplayMessageActionsFromList(this TaskDefinition td, Dictionary<string, object> additionalActions) // Renamed method
        {
            try
            {
                // Process display message actions
                if (additionalActions.TryGetValue("DisplayMessageActions", out object displayMessageActionsObj) &&
                    displayMessageActionsObj is List<DisplayMessageAction> displayMessageActions)
                {
                    string wrapperLocation = Path.GetDirectoryName(td.Actions.Count > 0 && td.Actions[0] is ExecAction execAction
                        ? execAction.WorkingDirectory
                        : Environment.GetFolderPath(Environment.SpecialFolder.System));
                    
                    foreach (var displayMessageAction in displayMessageActions)
                    {
                        // Use the DisplayMessageActionSupport to add the action
                        DisplayMessageActionSupport.AddDisplayMessageAction(td, 
                            displayMessageAction.Title, 
                            displayMessageAction.Message, 
                            wrapperLocation);
                    }
                }
                
                // Process other action types as needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding multiple actions: {ex.Message}");
            }
        }
    }
}
