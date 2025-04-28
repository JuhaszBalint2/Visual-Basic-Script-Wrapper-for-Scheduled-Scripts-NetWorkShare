using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ScriptSchedulerApp
{
    /// <summary>
    /// Utility class to run scripts completely hidden with no console window
    /// </summary>
    public static class HiddenScriptRunner
    {
        // Win32 constants for CreateProcess
        private const int NORMAL_PRIORITY_CLASS = 0x0020;
        private const int CREATE_NO_WINDOW = 0x08000000;
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);
            
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }
        
        // Constants for dwFlags in STARTUPINFO
        private const uint STARTF_USESHOWWINDOW = 0x00000001;
        private const short SW_HIDE = 0;
        
        /// <summary>
        /// Runs a script completely hidden with no console window using Win32 API
        /// </summary>
        public static void RunScriptHidden(string scriptPath, string scriptType, string args, string workingDir, string logDir)
        {
            string fullCommand;
            
            // Create log directory if it doesn't exist
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            
            // Create logfile path
            string logFileName = $"{Path.GetFileNameWithoutExtension(scriptPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            string logFilePath = Path.Combine(logDir, logFileName);
            
            // Log initial info
            if (!string.IsNullOrEmpty(logDir))
            {
                StringBuilder logBuilder = new StringBuilder();
                logBuilder.AppendLine($"{DateTime.Now} - Script execution started");
                logBuilder.AppendLine($"{DateTime.Now} - Script path: {scriptPath}");
                logBuilder.AppendLine($"{DateTime.Now} - Arguments: {args}");
                logBuilder.AppendLine($"{DateTime.Now} - Working directory: {workingDir}");
                File.WriteAllText(logFilePath, logBuilder.ToString());
            }
            
            // Get command parameters using TaskWrapper to avoid duplicate logic
            var (executable, arguments) = TaskWrapper.GetHiddenExecutionCommand(scriptPath, scriptType, args);
            
            // Create full command line
            fullCommand = $"\"{executable}\" {arguments}";
            
            try
            {
                // Setup STARTUPINFO for hidden window
                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = SW_HIDE;
                
                // Initialize PROCESS_INFORMATION
                PROCESS_INFORMATION pi;
                
                // Create the process completely hidden with no console window
                bool result = CreateProcess(
                    null,
                    fullCommand,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CREATE_NO_WINDOW | NORMAL_PRIORITY_CLASS,
                    IntPtr.Zero,
                    workingDir,
                    ref si,
                    out pi);
                
                if (result)
                {
                    // Log success
                    if (!string.IsNullOrEmpty(logDir))
                    {
                        File.AppendAllText(logFilePath, $"{DateTime.Now} - Process started successfully with command: {fullCommand}\r\n");
                    }
                }
                else
                {
                    // Log failure
                    int error = Marshal.GetLastWin32Error();
                    if (!string.IsNullOrEmpty(logDir))
                    {
                        File.AppendAllText(logFilePath, $"{DateTime.Now} - Failed to start process: Error code {error}\r\n");
                    }
                    throw new System.ComponentModel.Win32Exception(error);
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions
                if (!string.IsNullOrEmpty(logDir))
                {
                    File.AppendAllText(logFilePath, $"{DateTime.Now} - Exception: {ex.Message}\r\n");
                }
                throw;
            }
        }
    }
}