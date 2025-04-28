using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Text;

namespace ScriptSchedulerApp
{
    public static class CredentialManagerHelper
    {
        // P/Invoke constants from wincred.h
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2; // Persist across logons for the local machine

        // P/Invoke structure for CREDENTIAL
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        // P/Invoke function CredRead
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, int type, int flags, out IntPtr credentialPtr);

        // P/Invoke function CredWrite
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] int flags);

        // P/Invoke function CredFree
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CredFree([In] IntPtr cred);

        /// <summary>
        /// Retrieves generic credentials for a specific target from the Windows Credential Manager.
        /// </summary>
        /// <param name="target">The target name (e.g., server name like "192.168.1.238" or "JuhaszNAS1").</param>
        /// <param name="userName">Output parameter for the username associated with the credential.</param>
        /// <param name="password">Output parameter for the password associated with the credential.</param>
        /// <returns>True if credentials were found and retrieved successfully, false otherwise.</returns>
        public static bool GetCredential(string target, out string userName, out string password)
        {
            userName = null;
            password = null;
            IntPtr credPtr = IntPtr.Zero;

            try
            {
                // Read the credential
                if (!CredRead(target, CRED_TYPE_GENERIC, 0, out credPtr))
                {
                    // Credential not found is a common case (ERROR_NOT_FOUND = 1168)
                    int error = Marshal.GetLastWin32Error();
                    if (error == 1168) // ERROR_NOT_FOUND
                    {
                        System.Diagnostics.Debug.WriteLine($"Credential not found for target: {target}");
                        return false;
                    }
                    else
                    {
                        // Throw for other errors
                        throw new Win32Exception(error);
                    }
                }

                // Marshal the pointer to the structure
                CREDENTIAL credential = (CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));

                // Get username
                userName = credential.UserName;

                // Get password - CredentialBlob is ANSI bytes, need to convert
                if (credential.CredentialBlobSize > 0 && credential.CredentialBlob != IntPtr.Zero)
                {
                    byte[] passwordBytes = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, credential.CredentialBlobSize);
                    // Assuming UTF-8 encoding for generic credentials. Adjust if needed.
                    // Note: Passwords might be stored differently; this is a common way.
                    password = Encoding.UTF8.GetString(passwordBytes);
                    // Trim potential null terminators if necessary
                    password = password.TrimEnd('\0');
                }
                else
                {
                    password = string.Empty; // No password blob stored
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading credential for target '{target}': {ex.Message}");
                return false;
            }
            finally
            {
                // Free the credential buffer
                if (credPtr != IntPtr.Zero)
                {
                    CredFree(credPtr);
                }
            }
        }

        /// <summary>
        /// Saves a generic credential to the Windows Credential Manager.
        /// </summary>
        /// <param name="target">The target name (e.g., server name).</param>
        /// <param name="userName">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="persistence">Persistence type (e.g., CRED_PERSIST_LOCAL_MACHINE).</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool SaveCredential(string target, string userName, string password, int persistence = CRED_PERSIST_LOCAL_MACHINE)
        {
            try
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                IntPtr passwordBlob = Marshal.AllocHGlobal(passwordBytes.Length);
                Marshal.Copy(passwordBytes, 0, passwordBlob, passwordBytes.Length);

                CREDENTIAL credential = new CREDENTIAL
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = target,
                    UserName = userName,
                    CredentialBlob = passwordBlob,
                    CredentialBlobSize = passwordBytes.Length,
                    Persist = persistence,
                    AttributeCount = 0, // No attributes
                    Attributes = IntPtr.Zero
                };

                bool success = CredWrite(ref credential, 0);

                Marshal.FreeHGlobal(passwordBlob); // Free allocated memory

                if (!success)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                System.Diagnostics.Debug.WriteLine($"Credential saved successfully for target: {target}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credential for target '{target}': {ex.Message}");
                return false;
            }
        }
    }
}