using System;
using System.Collections.Generic;
using Microsoft.Win32.TaskScheduler;
using System.Net.Mail;
using System.Text;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Extensions to the ScriptManager class for supporting additional Windows Task Scheduler options
    /// </summary>
    public static class TaskSchedulerExtensions
    {
        /// <summary>
        /// Enhances a TaskDefinition with additional options from the Windows Task Scheduler
        /// </summary>
        /// <param name="td">The TaskDefinition to enhance</param>
        /// <param name="options">Dictionary of additional options</param>
        public static void EnhanceTaskDefinition(this TaskDefinition td, Dictionary<string, object> options)
        {
            // Process settings
            if (options.TryGetValue("AllowDemandStart", out object allowDemandStartObj) && allowDemandStartObj is bool allowDemandStart)
            {
                td.Settings.AllowDemandStart = allowDemandStart;
            }

            if (options.TryGetValue("RunOnlyIfIdle", out object runOnlyIfIdleObj) && runOnlyIfIdleObj is bool runOnlyIfIdle)
            {
                td.Settings.RunOnlyIfIdle = runOnlyIfIdle;
            }

            if (options.TryGetValue("IdleMinutes", out object idleMinutesObj) && int.TryParse(idleMinutesObj.ToString(), out int idleMinutes))
            {
                td.Settings.IdleSettings.IdleDuration = TimeSpan.FromMinutes(idleMinutes);
            }

            if (options.TryGetValue("IdleWaitTimeout", out object idleWaitTimeoutObj) && int.TryParse(idleWaitTimeoutObj.ToString(), out int idleWaitTimeout))
            {
                td.Settings.IdleSettings.WaitTimeout = TimeSpan.FromMinutes(idleWaitTimeout);
            }

            if (options.TryGetValue("StopIfGoingOnBatteries", out object stopIfGoingOnBatteriesObj) && stopIfGoingOnBatteriesObj is bool stopIfGoingOnBatteries)
            {
                td.Settings.StopIfGoingOnBatteries = stopIfGoingOnBatteries;
            }

            if (options.TryGetValue("DisallowStartIfOnBatteries", out object disallowStartIfOnBatteriesObj) && disallowStartIfOnBatteriesObj is bool disallowStartIfOnBatteries)
            {
                td.Settings.DisallowStartIfOnBatteries = disallowStartIfOnBatteries;
            }

            if (options.TryGetValue("WakeToRun", out object wakeToRunObj) && wakeToRunObj is bool wakeToRun)
            {
                td.Settings.WakeToRun = wakeToRun;
            }

            if (options.TryGetValue("RunIfMissed", out object runIfMissedObj) && runIfMissedObj is bool runIfMissed)
            {
                td.Settings.StartWhenAvailable = runIfMissed;
            }

            if (options.TryGetValue("MultipleInstances", out object multipleInstancesObj) && int.TryParse(multipleInstancesObj.ToString(), out int multipleInstances))
            {
                td.Settings.MultipleInstances = (TaskInstancesPolicy)multipleInstances;
            }

            if (options.TryGetValue("DeleteWhenExpired", out object deleteWhenExpiredObj) && deleteWhenExpiredObj is bool deleteWhenExpired)
            {
                td.Settings.DeleteExpiredTaskAfter = deleteWhenExpired ? TimeSpan.FromDays(30) : TimeSpan.Zero;
            }

            if (options.TryGetValue("ExecutionTimeLimit", out object executionTimeLimitObj) && int.TryParse(executionTimeLimitObj.ToString(), out int executionTimeLimit))
            {
                td.Settings.ExecutionTimeLimit = executionTimeLimit <= 0 ? TimeSpan.Zero : TimeSpan.FromHours(executionTimeLimit);
            }

            if (options.TryGetValue("Enabled", out object enabledObj) && enabledObj is bool enabled)
            {
                td.Settings.Enabled = enabled;
            }

            if (options.TryGetValue("Hidden", out object hiddenObj) && hiddenObj is bool hidden)
            {
                td.Settings.Hidden = hidden;
            }

            // Network settings
            if (options.TryGetValue("RunOnlyIfNetworkAvailable", out object runOnlyIfNetworkAvailableObj) && runOnlyIfNetworkAvailableObj is bool runOnlyIfNetworkAvailable)
            {
                td.Settings.RunOnlyIfNetworkAvailable = runOnlyIfNetworkAvailable;
            }

            if (options.TryGetValue("NetworkId", out object networkIdObj) && networkIdObj is string networkId && !string.IsNullOrEmpty(networkId))
            {
                // NetworkSettings.Id expects a Guid, so we need to parse the string
                if (Guid.TryParse(networkId, out Guid networkGuid))
                {
                    td.Settings.NetworkSettings.Id = networkGuid;
                }
            }

            if (options.TryGetValue("NetworkName", out object networkNameObj) && networkNameObj is string networkName && !string.IsNullOrEmpty(networkName))
            {
                td.Settings.NetworkSettings.Name = networkName;
            }

            // Restart on failure settings
            if (options.TryGetValue("RestartOnFailure", out object restartOnFailureObj) && restartOnFailureObj is bool restartOnFailure && restartOnFailure)
            {
                if (options.TryGetValue("RestartCount", out object restartCountObj) && int.TryParse(restartCountObj.ToString(), out int restartCount) && restartCount > 0)
                {
                    td.Settings.RestartCount = restartCount;
                }

                if (options.TryGetValue("RestartInterval", out object restartIntervalObj) && int.TryParse(restartIntervalObj.ToString(), out int restartInterval) && restartInterval > 0)
                {
                    td.Settings.RestartInterval = TimeSpan.FromMinutes(restartInterval);
                }
            }

            // Run level and logon type
            if (options.TryGetValue("RunLevel", out object runLevelObj) && int.TryParse(runLevelObj.ToString(), out int runLevel))
            {
                td.Principal.RunLevel = (TaskRunLevel)runLevel; // 0 = LUA, 1 = Highest
            }

            if (options.TryGetValue("LogonType", out object logonTypeObj) && int.TryParse(logonTypeObj.ToString(), out int logonType))
            {
                td.Principal.LogonType = (TaskLogonType)logonType;
            }

            // Process Priority
            if (options.TryGetValue("Priority", out object priorityObj) && int.TryParse(priorityObj.ToString(), out int priority))
            {
                td.Settings.Priority = (System.Diagnostics.ProcessPriorityClass)priority;
            }
            
            // Configure force stop
            if (options.TryGetValue("ForceStop", out object forceStopObj) && forceStopObj is bool forceStop)
            {
                td.Settings.AllowHardTerminate = forceStop;
            }
            
            // Task compatibility level (which Windows versions the task is compatible with)
            if (options.TryGetValue("Compatibility", out object compatibilityObj) && int.TryParse(compatibilityObj.ToString(), out int compatibility))
            {
                td.Settings.Compatibility = (TaskCompatibility)compatibility;
            }
            
            // Additional advanced settings
            // Do not store password option
            if (options.TryGetValue("DoNotStorePassword", out object doNotStorePasswordObj) && doNotStorePasswordObj is bool doNotStorePassword)
            {
                td.Principal.LogonType = doNotStorePassword ? TaskLogonType.S4U : td.Principal.LogonType;
            }
            
            // Run whether user is logged on or not
            if (options.TryGetValue("RunWhetherLoggedOn", out object runWhetherLoggedOnObj) && runWhetherLoggedOnObj is bool runWhetherLoggedOn)
            {
                td.Principal.LogonType = runWhetherLoggedOn ? TaskLogonType.Password : TaskLogonType.InteractiveToken;
            }
        }

        /// <summary>
        /// Creates a SendEmail action for a task definition
        /// </summary>
        /// <param name="td">The task definition to enhance</param>
        /// <param name="from">Sender email address</param>
        /// <param name="to">Recipient email address(es)</param>
        /// <param name="subject">Email subject</param>
        /// <param name="body">Email body</param>
        /// <param name="server">SMTP server</param>
        /// <param name="port">SMTP port</param>
        /// <param name="useSSL">Whether to use SSL for SMTP connection</param>
        /// <returns>The added email action</returns>
        public static EmailAction CreateEmailAction(this TaskDefinition td, string from, string to, string subject, string body, string server, int port = 25, bool useSSL = true)
        {
            // Create a new email action
            var emailAction = new EmailAction
            {
                From = from,
                To = to,
                Subject = subject,
                Body = body,
                Server = server
            };
            
            // Set up secure connection if needed
            if (useSSL)
            {
                emailAction.HeaderFields.Add("X-UseSSL", "true");
            }
            
            // Set port if not default
            if (port != 25)
            {
                emailAction.HeaderFields.Add("X-Port", port.ToString());
            }
            
            // Add the action to the task definition
            td.Actions.Add(emailAction);
            
            return emailAction;
        }
        
        /// <summary>
        /// Creates a display message action for a task definition
        /// </summary>
        /// <param name="td">The task definition to enhance</param>
        /// <param name="title">Title of the message</param>
        /// <param name="message">Content of the message</param>
        /// <returns>The added show message action</returns>
        public static ShowMessageAction CreateDisplayMessageAction(this TaskDefinition td, string title, string message)
        {
            // Create a new display message action
            var messageAction = new ShowMessageAction(title, message);
            
            // Add the action to the task definition
            td.Actions.Add(messageAction);
            
            return messageAction;
        }
        
        /// <summary>
        /// Creates an IdleTrigger for the task definition
        /// </summary>
        /// <param name="td">The task definition to modify</param>
        /// <param name="startBoundary">Optional start boundary for the trigger</param>
        /// <param name="endBoundary">Optional end boundary for the trigger</param>
        /// <returns>The created trigger</returns>
        public static IdleTrigger CreateIdleTrigger(this TaskDefinition td, DateTime? startBoundary = null, DateTime? endBoundary = null)
        {
            var trigger = new IdleTrigger();
            
            if (startBoundary.HasValue)
                trigger.StartBoundary = startBoundary.Value;
                
            if (endBoundary.HasValue)
                trigger.EndBoundary = endBoundary.Value;
                
            td.Triggers.Add(trigger);
            return trigger;
        }
        
        /// <summary>
        /// Creates an EventTrigger for the task definition
        /// </summary>
        /// <param name="td">The task definition to modify</param>
        /// <param name="logName">Event log name (e.g., "System", "Application")</param>
        /// <param name="source">Source of the event (e.g., "Application Error")</param>
        /// <param name="eventId">Event ID to filter on. Use null for all events.</param>
        /// <param name="startBoundary">Optional start boundary for the trigger</param>
        /// <param name="endBoundary">Optional end boundary for the trigger</param>
        /// <returns>The created trigger</returns>
        public static EventTrigger CreateEventTrigger(this TaskDefinition td, string logName, string source = null, int? eventId = null, DateTime? startBoundary = null, DateTime? endBoundary = null)
        {
            var trigger = new EventTrigger();
            
            var query = new StringBuilder();
            query.Append("<QueryList><Query Id=\"0\" Path=\"").Append(logName).Append("\"><Select Path=\"").Append(logName).Append("\">");
            query.Append("*");
            
            if (!string.IsNullOrEmpty(source))
            {
                query.Append("[System/Provider/@Name='").Append(source).Append("']");
            }
            
            if (eventId.HasValue)
            {
                query.Append("[System/EventID=").Append(eventId.Value).Append("]");
            }
            
            query.Append("</Select></Query></QueryList>");
            
            trigger.Subscription = query.ToString();
            
            if (startBoundary.HasValue)
                trigger.StartBoundary = startBoundary.Value;
                
            if (endBoundary.HasValue)
                trigger.EndBoundary = endBoundary.Value;
                
            td.Triggers.Add(trigger);
            return trigger;
        }
        
        /// <summary>
        /// Creates a SessionStateChangeTrigger for the task definition
        /// </summary>
        /// <param name="td">The task definition to modify</param>
        /// <param name="stateChange">The session state change that will trigger the task</param>
        /// <param name="userId">Specific user ID to monitor, or null for all users</param>
        /// <param name="startBoundary">Optional start boundary for the trigger</param>
        /// <param name="endBoundary">Optional end boundary for the trigger</param>
        /// <returns>The created trigger</returns>
        public static SessionStateChangeTrigger CreateSessionStateChangeTrigger(this TaskDefinition td, TaskSessionStateChangeType stateChange, string userId = null, DateTime? startBoundary = null, DateTime? endBoundary = null)
        {
            var trigger = new SessionStateChangeTrigger(stateChange);
            
            if (!string.IsNullOrEmpty(userId))
                trigger.UserId = userId;
                
            if (startBoundary.HasValue)
                trigger.StartBoundary = startBoundary.Value;
                
            if (endBoundary.HasValue)
                trigger.EndBoundary = endBoundary.Value;
                
            td.Triggers.Add(trigger);
            return trigger;
        }
        
        /// <summary>
        /// Creates a RegistrationTrigger for the task definition (runs on task creation/modification)
        /// </summary>
        /// <param name="td">The task definition to modify</param>
        /// <param name="delay">Delay before starting the task</param>
        /// <param name="startBoundary">Optional start boundary for the trigger</param>
        /// <param name="endBoundary">Optional end boundary for the trigger</param>
        /// <returns>The created trigger</returns>
        public static RegistrationTrigger CreateRegistrationTrigger(this TaskDefinition td, TimeSpan? delay = null, DateTime? startBoundary = null, DateTime? endBoundary = null)
        {
            var trigger = new RegistrationTrigger();
            
            if (delay.HasValue)
                trigger.Delay = delay.Value;
                
            if (startBoundary.HasValue)
                trigger.StartBoundary = startBoundary.Value;
                
            if (endBoundary.HasValue)
                trigger.EndBoundary = endBoundary.Value;
                
            td.Triggers.Add(trigger);
            return trigger;
        }
        
        /// <summary>
        /// Adds repetition pattern to an existing trigger
        /// </summary>
        /// <param name="trigger">The trigger to modify</param>
        /// <param name="interval">The interval between repetitions</param>
        /// <param name="duration">The duration of the repetition pattern, or TimeSpan.Zero for indefinite</param>
        /// <param name="stopAtDurationEnd">Whether to stop at the end of the duration</param>
        /// <returns>The modified trigger</returns>
        public static Trigger AddRepetitionPattern(this Trigger trigger, TimeSpan interval, TimeSpan duration, bool stopAtDurationEnd = true)
        {
            trigger.Repetition.Interval = interval;
            trigger.Repetition.Duration = duration;
            trigger.Repetition.StopAtDurationEnd = stopAtDurationEnd;
            
            return trigger;
        }
    }
}