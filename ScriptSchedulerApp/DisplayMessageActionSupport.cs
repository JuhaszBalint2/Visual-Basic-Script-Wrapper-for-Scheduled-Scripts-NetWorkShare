using System;
using System.IO;
using System.Windows;
using Microsoft.Win32.TaskScheduler;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Support class for handling display message actions
    /// </summary>
    public static class DisplayMessageActionSupport
    {
        /// <summary>
        /// Creates a new display message action
        /// </summary>
        /// <param name="title">Title of the message</param>
        /// <param name="message">Content of the message</param>
        /// <param name="wrapperLocation">Location to store generated scripts</param>
        /// <returns>Path to the generated script</returns>
        public static string CreateDisplayMessageScript(string title, string message, string wrapperLocation)
        {
            try
            {
                // Make sure the wrapper location exists
                if (!Directory.Exists(wrapperLocation))
                {
                    Directory.CreateDirectory(wrapperLocation);
                }
                
                // Create a unique name for the script
                string scriptName = $"DisplayMessage_{DateTime.Now:yyyyMMdd_HHmmss}.vbs";
                string scriptPath = Path.Combine(wrapperLocation, scriptName);
                
                // Build the VBScript content
                // Avoiding raw string literals by using regular string concatenation
                string scriptContent = 
                    "'\r\n" +
                    "'----------------------------------------------------------------------\r\n" +
                    "' Display Message Script\r\n" +
                    "' Generated on: " + DateTime.Now + "\r\n" +
                    "'----------------------------------------------------------------------\r\n" +
                    "Option Explicit\r\n" +
                    "\r\n" +
                    "' Define constants for message box style and buttons\r\n" +
                    "Const MB_OK = 0\r\n" +
                    "Const MB_ICONINFORMATION = 64\r\n" +
                    "\r\n" +
                    "' Show the message box\r\n" +
                    "MsgBox \"" + message.Replace("\"", "\"\"") + "\", MB_OK + MB_ICONINFORMATION, \"" + title.Replace("\"", "\"\"") + "\" \r\n" +
                    "\r\n" +
                    "'----------------------------------------------------------------------\r\n" +
                    "' End of script\r\n" +
                    "'----------------------------------------------------------------------\r\n";

                // Write the script to disk
                File.WriteAllText(scriptPath, scriptContent);
                
                return scriptPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating display message script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        
        /// <summary>
        /// Adds a display message action to a task definition by creating a script
        /// </summary>
        /// <param name="td">The task definition to enhance</param>
        /// <param name="title">Title of the message</param>
        /// <param name="message">Message content</param>
        /// <param name="wrapperLocation">Location to store generated scripts</param>
        /// <returns>The created action</returns>
        public static ExecAction AddDisplayMessageAction(TaskDefinition td, string title, string message, string wrapperLocation)
        {
            string scriptPath = CreateDisplayMessageScript(title, message, wrapperLocation);
            
            if (string.IsNullOrEmpty(scriptPath))
            {
                return null;
            }
            
            // Create an action to run the script
            var action = new ExecAction("wscript.exe", $"\"{scriptPath}\"", Path.GetDirectoryName(scriptPath));
            
            // Add the action to the task definition
            td.Actions.Add(action);
            
            return action;
        }
    }
}