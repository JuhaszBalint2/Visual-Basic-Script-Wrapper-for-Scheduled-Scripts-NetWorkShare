using System;
using System.Windows;
using System.IO;

namespace ScriptSchedulerApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set up unhandled exception handling
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => 
                HandleException(args.ExceptionObject as Exception);
                
            Current.DispatcherUnhandledException += (sender, args) => 
            {
                HandleException(args.Exception);
                args.Handled = true;
            };
        }
        
        private void HandleException(Exception ex)
        {
            string message = $"An unexpected error occurred: {ex?.Message}";
            
            // Log the exception
            try
            {
                string logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScriptSchedulerApp", "Logs");
                
                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);
                    
                string logFile = Path.Combine(logFolder, $"Error_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, $"[{DateTime.Now}] {ex}\r\n\r\n");
            }
            catch
            {
                // Ignore logging errors
            }
            
            MessageBox.Show(message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}