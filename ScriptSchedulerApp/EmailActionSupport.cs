using System;
using System.Collections.Generic;
using Microsoft.Win32.TaskScheduler;
using System.Net.Mail;
using System.Text;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Extension methods for email actions in Windows Task Scheduler
    /// </summary>
    public static class EmailActionSupport
    {
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
        public static EmailAction AddEmailAction(this TaskDefinition td, string from, string to, string subject, string body, string server, int port = 25, bool useSSL = true)
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
        /// Creates a SendEmail action for task completion notification
        /// </summary>
        /// <param name="td">The task definition to enhance</param>
        /// <param name="from">Sender email address</param>
        /// <param name="to">Recipient email address(es)</param>
        /// <param name="taskName">Name of the task (for subject line)</param>
        /// <param name="server">SMTP server</param>
        /// <param name="port">SMTP port</param>
        /// <param name="useSSL">Whether to use SSL for SMTP connection</param>
        /// <returns>The added email action</returns>
        public static EmailAction AddTaskCompletionEmail(this TaskDefinition td, string from, string to, string taskName, string server, int port = 25, bool useSSL = true)
        {
            string subject = $"Task Completion: {taskName}";
            string body = $"The scheduled task '{taskName}' has completed execution at {DateTime.Now}.";
            
            return AddEmailAction(td, from, to, subject, body, server, port, useSSL);
        }
    }
}