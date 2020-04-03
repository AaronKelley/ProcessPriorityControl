using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// Utility methods for dealing with processes.
    /// </summary>
    static class ProcessUtility
    {
        /// <summary>
        /// Given a process, get the parent process.
        /// </summary>
        /// <param name="process">A Windows process</param>
        /// <returns>The process that created the provided process (NULL if unavailable)</returns>
        /// <seealso cref="https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way"/>
        public static Process Parent(this Process process)
        {
            ProcessBasicInformation processBasicInformation = new ProcessBasicInformation();
            int status = NtQueryInformationProcess(process.Handle, 0, ref processBasicInformation, Marshal.SizeOf(processBasicInformation), out _);

            if (status != 0)
            {
                throw new Exception("Failed to pull process parent information with status: " + status);
            }

            try
            {
                return Process.GetProcessById(processBasicInformation.InheritedFromUniqueProcessId.ToInt32());
            }
            catch (Exception)
            {
                // Not found.
                return null;
            }
        }

        /// <summary>
        /// Get the SID of the user that a given process is running under.
        /// </summary>
        /// <param name="process">A Windows process</param>
        /// <returns>User SID</returns>
        /// <seealso cref="https://www.codeproject.com/Articles/14828/How-To-Get-Process-Owner-ID-and-Current-User-SID"/>
        /// <seealso cref="https://bytes.com/topic/c-sharp/answers/225065-how-call-win32-native-api-gettokeninformation-using-c"/>
        public static string UserSid(this Process process)
        {
            string userSid = string.Empty;

            try
            {
                IntPtr sidPtr = DumpUserInfo(process.Handle);
                if (sidPtr != IntPtr.Zero)
                {
                    ConvertSidToStringSid(sidPtr, ref userSid);
                }
                else
                {
                    userSid = null;
                }
            }
            catch
            {
                userSid = null;
            }

            return userSid;
        }

        /// <summary>
        /// Determine whether it is likely that this process is hosting Windows services.
        /// </summary>
        /// <param name="process">A Windows process</param>
        /// <returns>True if this is likely a service host process, false otherwise</returns>
        public static bool IsServiceProcessCandidate(this Process process)
        {
            // Maybe expand on this later.
            return process.ProcessName == "svchost";
        }

        /// <summary>
        /// Get a list of names of services hosted by this process.
        /// </summary>
        /// <param name="process">A Windows process</param>
        /// <returns>List of names of services that this process is running</returns>
        /// <seealso cref="https://stackoverflow.com/questions/23084720/get-the-pid-of-a-windows-service"/>
        /// <seealso cref="https://stackoverflow.com/questions/565658/finding-out-windows-services-running-process-name-net-1-1/569288#569288"/>
        public static List<string> ServiceNames(this Process process)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Service WHERE ProcessId = " + process.Id);
            List<string> serviceNames = new List<string>();

            foreach (ManagementObject result in searcher.Get())
            {
                string serviceName = result["Name"].ToString();
                if (serviceName.Contains("_"))
                {
                    serviceName = serviceName.Substring(0, serviceName.IndexOf("_") + 1);
                }

                serviceNames.Add(serviceName);
            }

            return serviceNames;
        }

        /// <summary>
        /// Given a process handle, get a pointer to some information on the process.
        /// </summary>
        /// <param name="pToken">Process token</param>
        /// <returns>Pointer to information data structure</returns>
        private static IntPtr DumpUserInfo(IntPtr pToken)
        {
            int access = TokenQuery;
            IntPtr processToken = IntPtr.Zero;
            IntPtr result = IntPtr.Zero;

            try
            {
                if (OpenProcessToken(pToken, access, ref processToken))
                {
                    result = ProcessTokenToSid(processToken);
                    CloseHandle(processToken);
                }

                return result;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Given a process token, get a pointer to the user SID.
        /// </summary>
        /// <param name="token">Process token</param>
        /// <returns>Pointer to user SID</returns>
        private static IntPtr ProcessTokenToSid(IntPtr token)
        {
            TokenUser tokenUser;
            const int bufferLength = 256;
            IntPtr tu = Marshal.AllocHGlobal(bufferLength);
            IntPtr result = IntPtr.Zero;

            try
            {
                int cb = bufferLength;

                if (GetTokenInformation(token, TokenInformationClass.TokenUser, tu, cb, ref cb))
                {
                    tokenUser = (TokenUser)Marshal.PtrToStructure(tu, typeof(TokenUser));
                    result = tokenUser.User.Sid;
                }

                return result;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(tu);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // The following structs, enums, and external methods are used by code in this class to pull data from the Win32 API. //
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static readonly int TokenQuery = 0x00000008;

        enum TokenInformationClass
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessBasicInformation
        {
            internal IntPtr Reserved1;
            internal IntPtr PebBaseAddress;
            internal IntPtr Reserved2_0;
            internal IntPtr Reserved2_1;
            internal IntPtr UniqueProcessId;
            internal IntPtr InheritedFromUniqueProcessId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SidAndAttributes
        {
            public IntPtr Sid;
            public int Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TokenUser
        {
            public SidAndAttributes User;
        }

        [DllImport("advapi32", CharSet = CharSet.Auto)]
        static extern bool ConvertSidToStringSid(
            IntPtr pSID,
            [In, Out, MarshalAs(UnmanagedType.LPTStr)] ref string pStringSid
        );

        [DllImport("advapi32", CharSet = CharSet.Auto)]
        static extern bool GetTokenInformation(
            IntPtr hToken,
            TokenInformationClass tokenInfoClass,
            IntPtr TokenInformation,
            int tokeInfoLength,
            ref int reqLength
        );

        [DllImport("advapi32")]
        static extern bool OpenProcessToken(
            IntPtr ProcessHandle, // handle to process
            int DesiredAccess, // desired access to process
            ref IntPtr TokenHandle // handle to open access token
        );

        [DllImport("kernel32")]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ProcessBasicInformation processInformation,
            int processInformationLength,
            out int returnLength
        );
    }
}
