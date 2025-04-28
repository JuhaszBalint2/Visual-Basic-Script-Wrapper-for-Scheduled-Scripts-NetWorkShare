using System;
using System.Collections.Generic;
using Microsoft.Win32.TaskScheduler;
using System.IO;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Helper class for managing multiple actions in Windows Task Scheduler
    /// </summary>
    public static class TaskSchedulerActions
    {
        /// <summary>
        /// Adds multiple actions to a task definition
        /// </summary>
        /// <param name="td">The task definition to modify</param>
        /// <param name="actions">Dictionary of action parameters</param>
        public static void AddMultipleActions(this TaskDefinition td, Dictionary<string, object> actions)
        {
            // Check if the actions dictionary contains program actions
            if (actions.TryGetValue("ProgramActions", out object programActionsObj) && 
                programActionsObj is List<Dictionary<string, string>> programActions)
            {
                foreach (var actionParams in programActions)
                {
                    // Each program action needs at least a path
                    if (actionParams.TryGetValue("Path", out string path) && !string.IsNullOrEmpty(path))
                    {
                        string args = actionParams.TryGetValue("Arguments", out string arguments) ? arguments : "";
                        string workingDir = actionParams.TryGetValue("WorkingDirectory", out string dir) ? dir : Path.GetDirectoryName(path);
                        
                        // Add the program action
                        td.Actions.Add(new ExecAction(path, args, workingDir));
                    }
                }
            }
            
            // Check if the actions dictionary contains email actions
            if (actions.TryGetValue("EmailActions", out object emailActionsObj) && 
                emailActionsObj is List<Dictionary<string, object>> emailActions)
            {
                foreach (var actionParams in emailActions)
                {
                    // Email action needs to, from and server at minimum
                    if (actionParams.TryGetValue("From", out object fromObj) && fromObj is string from &&
                        actionParams.TryGetValue("To", out object toObj) && toObj is string to &&
                        actionParams.TryGetValue("Server", out object serverObj) && serverObj is string server)
                    {
                        string subject = actionParams.TryGetValue("Subject", out object subjectObj) && subjectObj is string subj ? subj : "Task Notification";
                        string body = actionParams.TryGetValue("Body", out object bodyObj) && bodyObj is string bod ? bod : "Task completed.";
                        int port = actionParams.TryGetValue("Port", out object portObj) && portObj is int p ? p : 25;
                        bool useSSL = actionParams.TryGetValue("UseSSL", out object sslObj) && sslObj is bool ssl && ssl;
                        
                        // Add the email action
                        td.AddEmailAction(from, to, subject, body, server, port, useSSL);
                    }
                }
            }
            
            // Display message actions
            if (actions.TryGetValue("MessageActions", out object messageActionsObj) && 
                messageActionsObj is List<Dictionary<string, string>> messageActions)
            {
                foreach (var actionParams in messageActions)
                {
                    if (actionParams.TryGetValue("Title", out string title) && 
                        actionParams.TryGetValue("Message", out string message))
                    {
                        // Add the show message action
                        td.Actions.Add(new ShowMessageAction(title, message));
                    }
                }
            }
        }
        
        /// <summary>
        /// Adds a display message action to a task
        /// </summary>
        /// <param name="td">The task definition to modify</param>
        /// <param name="title">Message title</param>
        /// <param name="message">Message content</param>
        /// <returns>The created action</returns>
        public static ShowMessageAction AddDisplayMessageAction(this TaskDefinition td, string title, string message)
        {
            var messageAction = new ShowMessageAction(title, message);
            td.Actions.Add(messageAction);
            return messageAction;
        }
    }
}