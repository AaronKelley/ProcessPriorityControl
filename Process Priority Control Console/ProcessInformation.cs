using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// This class collects some information about a Windows process.
    /// </summary>
    class ProcessInformation
    {
        /// <summary>
        /// The Windows process that is the base for this information.
        /// </summary>
        private readonly Process _process;

        /// <summary>
        /// Parent process ID, if we have it on hand.
        /// </summary>
        private readonly object _parentProcessId;

        /// <summary>
        /// Process identifier (PID).
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// A short name of this process (typically, the executable filename).
        /// </summary>
        public string ShortName { get; }

        /// <summary>
        /// Full path to the running process executable.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// User running this process.
        /// </summary>
        public UserInformation User { get; }

        /// <summary>
        /// A hash based on the full path to the executable.
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// List of names of services hosted by this process.
        /// </summary>
        public List<string> ServiceNames { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="process">A Windows process to collect information for</param>
        /// <param name="parentProcessId">Optional ID of the parent process, to save lookup time</param>
        public ProcessInformation(Process process, object parentProcessId = null)
        {
            _process = process;
            _parentProcessId = parentProcessId;

            ProcessId = process.Id;
            ShortName = process.ProcessName;

            FullPath = null;
            Hash = null;
            User = null;

            try
            {
                FullPath = process.MainModule.FileName;
                Hash = Utility.GetMd5HashPrefixed(FullPath.ToLower());
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Error getting full path - {0}: {1}", process.Id, exception.Message);
                throw exception;
            }

            try
            {
                User = GetUserInformation();
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Error getting user information - {0}: {1}", process.Id, exception.Message);
                throw exception;
            }

            try
            {
                if (process.IsServiceProcessCandidate())
                {
                    ServiceNames = process.ServiceNames();
                }
                else
                {
                    ServiceNames = null;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Error getting service information - {0}: {1}", process.Id, exception.Message);
                throw exception;
            }
        }

        public void RecordProcessInformation()
        {
            // Record information for this process.
            RegistryAccess.RecordProcessInformation(this);

            // Tie in information about the parent process.
            try
            {
                ProcessInformation parent;

                if (_parentProcessId != null)
                {
                    parent = new ProcessInformation(Process.GetProcessById(int.Parse(_parentProcessId.ToString())));
                }
                else
                {
                    parent = new ProcessInformation(_process.Parent());
                }

                RegistryAccess.RecordProcessParentInformation(this, parent);
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Unable to fetch parent details: {0}", exception.Message);
            }
        }

        /// <summary>
        /// Get a rules object that corresponds to this process.
        /// </summary>
        /// <returns>Process rules object</returns>
        public ProcessWithRules GetRulesObject()
        {
            List<string> userSids = new List<string>
            {
                User.Sid
            };

            return new ProcessWithRules(Hash, ShortName, FullPath, userSids, new List<string>());
        }

        /// <summary>
        /// Get the user under which the given process is executing
        /// </summary>
        /// <param name="process">A Windows process</param>
        /// <returns>User under which the process is executing</returns>
        /// <seealso cref="https://www.codeproject.com/Articles/14828/How-To-Get-Process-Owner-ID-and-Current-User-SID"/>
        private UserInformation GetUserInformation()
        {
            // TODO Maybe try the fast version using Win32 API.
            string sid = _process.UserSid();
            UserInformation information = new UserInformation(string.Empty, string.Empty, sid);
            UserUtility.FillUsernameFromSid(information);
            return information;
        }
    }
}
