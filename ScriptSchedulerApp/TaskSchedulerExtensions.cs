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
    }
}