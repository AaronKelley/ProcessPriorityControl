﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// This class manages retrieving information from and storing information into the Windows registry.
    /// </summary>
    static class RegistryAccess
    {
        /// <summary>
        /// Base path for registry configuration for this product.
        /// </summary>
        private static readonly string RegistryBasePath = @"HKEY_LOCAL_MACHINE\SOFTWARE\aaron-kelley.net\Process Priority Control";

        /// <summary>
        /// Base path for user information.
        /// </summary>
        private static readonly string UserInformationBasePath = RegistryBasePath + @"\Users";

        /// <summary>
        /// Base path for process information.
        /// </summary>
        private static readonly string ProcessInformationBasePath = RegistryBasePath + @"\Processes";

        /// <summary>
        /// Base path for service information.
        /// </summary>
        private static readonly string ServiceInformationBasePath = RegistryBasePath + @"\Services";

        /// <summary>
        /// Base path for rules.
        /// </summary>
        private static readonly string RulesBasePath = RegistryBasePath + @"\Rules";

        /// <summary>
        /// Base path for launch scripts.
        /// </summary>
        private static readonly string LaunchScriptsPath = RegistryBasePath + @"\Launch scripts";

        /// <summary>
        /// Value name to track changes between background process and admin process.
        /// </summary>
        private static readonly string ChangesMadeValueName = "ChangesMade";

        /// <summary>
        /// Value name for an optional low-power script (default operation mode).
        /// </summary>
        private static readonly string LowPowerScriptValueName = "LowPowerScript";

        /// <summary>
        /// Value name for an optional high-power script (for high-priority processes).
        /// </summary>
        private static readonly string HighPowerScriptValueName = "HighPowerScript";

        /// <summary>
        /// Value name for an optional per-process processor affinity mask.
        /// </summary>
        private static readonly string ProcessorAffinityValueName = "ProcessorAffinity";

        /// <summary>
        /// Value name for an optional per-process processor affinity mask.
        /// </summary>
        private static readonly string KeepHighPriorityValueName = "KeepHighPriority";

        /// <summary>
        /// Set up the initial structure for the data stored in the registry.
        /// </summary>
        public static void RegistrySetup()
        {
            // Create the top-level key.
            Registry.SetValue(RegistryBasePath, string.Empty, string.Empty);

            // Create main subkeys.
            Registry.SetValue(UserInformationBasePath, string.Empty, string.Empty);
            Registry.SetValue(ProcessInformationBasePath, string.Empty, string.Empty);
            Registry.SetValue(ServiceInformationBasePath, string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath, string.Empty, string.Empty);
            Registry.SetValue(LaunchScriptsPath, string.Empty, string.Empty);

            Registry.SetValue(RulesBasePath + @"\Full path", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Short name", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Partial", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Username", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Services", string.Empty, string.Empty);
        }

        /// <summary>
        /// Record information about a process in the registry.
        /// </summary>
        /// <param name="information">Process information to store</param>
        public static void RecordProcessInformation(ProcessInformation information)
        {
            // Base path for this process, specifically.
            string basePath = ProcessInformationBasePath + @"\" + information.Hash;

            // Create key for this process.
            Registry.SetValue(basePath, string.Empty, information.FullPath, RegistryValueKind.String);

            // Store the last time that this particular process was seen for the current user.
            if (information.User != null && information.User.Sid != string.Empty)
            {
                Registry.SetValue(basePath, information.User.Sid, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), RegistryValueKind.QWord);
            }

            // Store the process name.
            if (information.ShortName != string.Empty)
            {
                Registry.SetValue(basePath, "Name", information.ShortName, RegistryValueKind.String);
            }

            // Service information.
            if (information.ServiceNames != null)
            {
                foreach (string serviceName in information.ServiceNames)
                {
                    // Base path for this service.
                    string serviceBasePath = ServiceInformationBasePath + @"\" + serviceName;

                    // Create key for this service.
                    Registry.SetValue(serviceBasePath, string.Empty, information.FullPath, RegistryValueKind.String);

                    // Store the last time that this particular service was observed.
                    Registry.SetValue(serviceBasePath, "Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), RegistryValueKind.QWord);
                }
            }

            // Record user information as well.
            RecordUserInformation(information.User);
        }

        /// <summary>
        /// Record information about the parent of a process in the registry.
        /// </summary>
        /// <param name="information">Information about the base process</param>
        /// <param name="parentInformation">Information about the parent process</param>
        public static void RecordProcessParentInformation(ProcessInformation information, ProcessInformation parentInformation)
        {
            string basePath = ProcessInformationBasePath + @"\" + information.Hash + @"\" + parentInformation.Hash;
            Registry.SetValue(basePath, string.Empty, parentInformation.FullPath, RegistryValueKind.String);
            Registry.SetValue(basePath, "Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), RegistryValueKind.QWord);

            // Store the process name.
            if (parentInformation.ShortName != string.Empty)
            {
                Registry.SetValue(basePath, "Name", parentInformation.ShortName, RegistryValueKind.String);
            }
        }

        /// <summary>
        /// Record information about a user in the registry.
        /// </summary>
        /// <param name="information">User information to store</param>
        public static void RecordUserInformation(UserInformation information)
        {
            if (information != null && information.Sid != string.Empty)
            {
                // Base path for this user, specifically.
                string basePath = UserInformationBasePath + @"\" + information.Sid;

                // Create key for this user.
                Registry.SetValue(basePath, string.Empty, string.Empty);

                // Store username and domain information.
                if (information.Username != string.Empty)
                {
                    Registry.SetValue(basePath, "Username", information.Username, RegistryValueKind.String);
                }

                if (information.Domain != string.Empty)
                {
                    Registry.SetValue(basePath, "Domain", information.Domain, RegistryValueKind.String);
                }
            }
        }

        /// <summary>
        /// Given a user SID, get the username.
        /// </summary>
        /// <param name="sid">User SID</param>
        /// <returns>Username</returns>
        public static string GetUsernameFromSid(string sid)
        {
            return Registry.GetValue(UserInformationBasePath + @"\" + sid, "Domain", null)?.ToString() + @"\" +
                   Registry.GetValue(UserInformationBasePath + @"\" + sid, "Username", null)?.ToString();
        }

        /// <summary>
        /// Get a list of all processes that have been observed.
        /// </summary>
        /// <returns>List of processes</returns>
        public static List<ProcessWithRules> GetObservedProcesses()
        {
            List<ProcessWithRules> processes = new List<ProcessWithRules>();

            RegistryKey processesKey = Registry.LocalMachine.OpenSubKey(ProcessInformationBasePath.Replace(@"HKEY_LOCAL_MACHINE\", string.Empty));
            foreach (string hash in processesKey.GetSubKeyNames())
            {
                RegistryKey processKey = Registry.LocalMachine.OpenSubKey(ProcessInformationBasePath.Replace(@"HKEY_LOCAL_MACHINE\", string.Empty) + @"\" + hash);

                // Short name.
                string shortName = Registry.GetValue(ProcessInformationBasePath + @"\" + hash, "Name", null)?.ToString();

                // Full path.
                string fullPath = Registry.GetValue(ProcessInformationBasePath + @"\" + hash, null, null)?.ToString();

                // Usernames.
                List<string> usernames = new List<string>();
                foreach (string value in processKey.GetValueNames())
                {
                    if (value.StartsWith("S-"))
                    {
                        usernames.Add(value);
                    }
                }

                // Parent process short names.
                List<string> parentProcessShortNames = new List<string>();
                foreach (string key in processKey.GetSubKeyNames())
                {
                    string parentName = Registry.GetValue(ProcessInformationBasePath + @"\" + hash + @"\" + key, "Name", null)?.ToString();
                    if (parentName != string.Empty)
                    {
                        parentProcessShortNames.Add(parentName);
                    }
                }

                processes.Add(new ProcessWithRules(hash, shortName, fullPath, usernames, parentProcessShortNames));
            }

            return processes;
        }

        public static List<string> GetObservedServiceNames()
        {
            List<string> serviceNames = new List<string>();

            RegistryKey servicesKey = Registry.LocalMachine.OpenSubKey(ServiceInformationBasePath.Replace(@"HKEY_LOCAL_MACHINE\", string.Empty));
            foreach (string serviceName in servicesKey.GetSubKeyNames())
            {
                serviceNames.Add(serviceName);
            }

            return serviceNames;
        }

        /// <summary>
        /// Given a process, get the full path rule (if one exists)
        /// </summary>
        /// <param name="process"></param>
        /// <returns>Full path rule, NULL if none</returns>
        public static Priority? GetFullPathRule(ProcessWithRules process)
        {
            object result = Registry.GetValue(RulesBasePath + @"\Full path", process.Hash, null);

            if (result != null)
            {
                int priority = int.Parse(result.ToString());
                return (Priority)priority;
            }

            return null;
        }

        /// <summary>
        /// Set a full path rule for a given process.
        /// </summary>
        /// <param name="process">A process</param>
        /// <param name="priority">Priority to set</param>
        public static void SetFullPathRule(ProcessWithRules process, Priority priority)
        {
            Registry.SetValue(RulesBasePath + @"\Full path", process.Hash, priority, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Given a process, get the short name rule (if one exists)
        /// </summary>
        /// <param name="process"></param>
        /// <returns>Short name rule, NULL if none</returns>
        public static Priority? GetShortNameRule(ProcessWithRules process)
        {
            object result = Registry.GetValue(RulesBasePath + @"\Short name", process.ShortName, null);

            if (result != null)
            {
                int priority = int.Parse(result.ToString());
                return (Priority)priority;
            }

            return null;
        }

        /// <summary>
        /// Set a short name rule for a given process.
        /// </summary>
        /// <param name="process">A process</param>
        /// <param name="priority">Priority to set</param>
        public static void SetShortNameRule(ProcessWithRules process, Priority priority)
        {
            Registry.SetValue(RulesBasePath + @"\Short name", process.ShortName, priority, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Given a process, get the partial path rule (if one exists)
        /// </summary>
        /// <param name="process"></param>
        /// <returns>Partial path rule, NULL if none</returns>
        public static Priority? GetPartialRule(ProcessWithRules process)
        {
            RegistryKey partialsKey = Registry.LocalMachine.OpenSubKey(RulesBasePath.Replace(@"HKEY_LOCAL_MACHINE\", string.Empty) + @"\Partial");
            foreach (string partialHash in partialsKey.GetSubKeyNames())
            {
                string partial = Registry.GetValue(RulesBasePath + @"\Partial\" + partialHash, string.Empty, null)?.ToString();
                if (partial != string.Empty && process.FullPath.ToLower().Contains(partial.ToLower()))
                {
                    object result = Registry.GetValue(RulesBasePath + @"\Partial\" + partialHash, "Priority", null);

                    if (result != null)
                    {
                        int priority = int.Parse(result.ToString());
                        return (Priority)priority;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Set a partial path rule for a given process.
        /// </summary>
        /// <param name="partialPath">Partial path as a string</param>
        /// <param name="priority">Priority to set</param>
        public static void SetPartialRule(string partialPath, Priority priority)
        {
            string hash = Utility.GetMd5HashPrefixed(partialPath.ToLower());

            Registry.SetValue(RulesBasePath + @"\Partial\" + hash, string.Empty, partialPath, RegistryValueKind.String);
            Registry.SetValue(RulesBasePath + @"\Partial\" + hash, "Priority", priority, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Given a process, get the username rule (if one exists)
        /// </summary>
        /// <param name="process"></param>
        /// <returns>Username rule, NULL if none</returns>
        public static Priority? GetUsernameRule(string username)
        {
            object result = Registry.GetValue(RulesBasePath + @"\Username", username, null);

            if (result != null)
            {
                int priority = int.Parse(result.ToString());
                return (Priority)priority;
            }

            return null;
        }

        /// <summary>
        /// Set a username rule for a given process.
        /// </summary>
        /// <param name="process">A process</param>
        /// <param name="priority">Priority to set</param>
        public static void SetUsernameRule(ProcessWithRules process, Priority priority)
        {
            if (process.UserSids.Count != 1)
            {
                throw new Exception("This rule requires that exactly one user is in the username list.");
            }

            Registry.SetValue(RulesBasePath + @"\Username", process.UserSids[0], priority, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Write to the registry that changes have been made to the rules.
        /// </summary>
        public static void SetChangesMade()
        {
            Registry.SetValue(RegistryBasePath, ChangesMadeValueName, 1, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Clear the flag in the registry that indicates that changes have been made to the rules.
        /// </summary>
        public static void ClearChangesMade()
        {
            Registry.SetValue(RegistryBasePath, ChangesMadeValueName, 0, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Check the registry to determine whether or not changes have been made to the rules.
        /// </summary>
        /// <returns>True if changes have been made, false otherwise</returns>
        public static bool GetChangesMade()
        {
            string result = Registry.GetValue(RegistryBasePath, ChangesMadeValueName, null)?.ToString();
            return result == "1";
        }

        /// <summary>
        /// Get the path to the low-power script, if one exists.
        /// </summary>
        /// <returns>Path to the low-power script; NULL if not configured</returns>
        public static string GetLowPowerScript()
        {
            string result = Registry.GetValue(RegistryBasePath, LowPowerScriptValueName, null)?.ToString();
            return result == string.Empty ? null : result;
        }

        /// <summary>
        /// Get the path to the high-power script, if one exists.
        /// </summary>
        /// <returns>Path to the high-power script; NULL if not configured</returns>
        public static string GetHighPowerScript()
        {
            string result = Registry.GetValue(RegistryBasePath, HighPowerScriptValueName, null)?.ToString();
            return result == string.Empty ? null : result;
        }

        /// <summary>
        /// Get the launch script for a process, if one exists.
        /// </summary>
        /// <param name="information">Information about the process</param>
        /// <returns>Launch script; NULL if none</returns>
        public static string GetLaunchScript(ProcessInformation information)
        {
            string result = Registry.GetValue(LaunchScriptsPath, information.Hash, null)?.ToString();
            return result == string.Empty ? null : result;
        }

        /// <summary>
        /// Get the priority set for a service, if one exists.
        /// </summary>
        /// <param name="serviceName">Service name</param>
        /// <returns>Priority that has been set; NULL if none</returns>
        public static Priority? GetPriorityForService(string serviceName)
        {
            string result = Registry.GetValue(RulesBasePath + @"\Services", serviceName, null)?.ToString();
            if (result == string.Empty || result == null)
            {
                return null;
            }
            else
            {
                return (Priority)int.Parse(result);
            }
        }

        /// <summary>
        /// Set the priority for a service.
        /// </summary>
        /// <param name="serviceName">Service name</param>
        /// <param name="priority">Priority to set</param>
        public static void SetPriorityForService(string serviceName, Priority priority)
        {
            Registry.SetValue(RulesBasePath + @"\Services", serviceName, priority, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Get the preferred processor affinity for a given process.
        /// </summary>
        /// <param name="process">A process</param>
        /// <returns>Preferred processor affinity mask</returns>
        public static long? GetProcessAffinity(ProcessWithRules process)
        {
            object result = Registry.GetValue(ProcessInformationBasePath + @"\" + process.Hash, ProcessorAffinityValueName, null);

            if (result != null)
            {
                long affinity = long.Parse(result.ToString());
                return affinity;
            }

            return null;
        }

        /// <summary>
        /// Determine whether this process has been assigned "keep high priority" status.
        /// </summary>
        /// <param name="process">A process</param>
        /// <returns>True if the process should be kept at high priority</returns>
        public static bool IsKeepHighPriorityProcess(ProcessWithRules process)
        {
            object result = Registry.GetValue(ProcessInformationBasePath + @"\" + process.Hash, KeepHighPriorityValueName, null);

            if (result != null)
            {
                long keepHighPriority = long.Parse(result.ToString());
                return keepHighPriority == 1;
            }

            return false;
        }
    }
}
