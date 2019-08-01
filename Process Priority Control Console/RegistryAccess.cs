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
        /// Base path for rules.
        /// </summary>
        private static readonly string RulesBasePath = RegistryBasePath + @"\Rules";

        /// <summary>
        /// Value name to track changes between background process and admin process.
        /// </summary>
        private static readonly string ChangesMade = "ChangesMade";

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
            Registry.SetValue(RulesBasePath, string.Empty, string.Empty);

            Registry.SetValue(RulesBasePath + @"\Full path", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Short name", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Partial", string.Empty, string.Empty);
            Registry.SetValue(RulesBasePath + @"\Username", string.Empty, string.Empty);
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

        public static string GetUsernameFromSid(string sid)
        {
            return Registry.GetValue(UserInformationBasePath + @"\" + sid, "Domain", null)?.ToString() + @"\" +
                   Registry.GetValue(UserInformationBasePath + @"\" + sid, "Username", null)?.ToString();
        }

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

        public static void SetFullPathRule(ProcessWithRules process, Priority priority)
        {
            Registry.SetValue(RulesBasePath + @"\Full path", process.Hash, priority, RegistryValueKind.DWord);
        }

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

        public static void SetShortNameRule(ProcessWithRules process, Priority priority)
        {
            Registry.SetValue(RulesBasePath + @"\Short name", process.ShortName, priority, RegistryValueKind.DWord);
        }

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

        public static void SetPartialRule(string partialPath, Priority priority)
        {
            string hash = Utility.GetMd5HashPrefixed(partialPath.ToLower());

            Registry.SetValue(RulesBasePath + @"\Partial\" + hash, string.Empty, partialPath, RegistryValueKind.String);
            Registry.SetValue(RulesBasePath + @"\Partial\" + hash, "Priority", priority, RegistryValueKind.DWord);
        }

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

        public static void SetUsernameRule(ProcessWithRules process, Priority priority)
        {
            if (process.UserSids.Count != 1)
            {
                throw new Exception("This rule requires that exactly one user is in the username list.");
            }

            Registry.SetValue(RulesBasePath + @"\Username", process.UserSids[0], priority, RegistryValueKind.DWord);
        }

        public static void SetChangesMade()
        {
            Registry.SetValue(RegistryBasePath, ChangesMade, 1, RegistryValueKind.DWord);
        }

        public static void ClearChangesMade()
        {
            Registry.SetValue(RegistryBasePath, ChangesMade, 0, RegistryValueKind.DWord);
        }

        public static bool GetChangesMade()
        {
            string result = Registry.GetValue(RegistryBasePath, ChangesMade, null)?.ToString();
            return result == "1";
        }
    }
}
