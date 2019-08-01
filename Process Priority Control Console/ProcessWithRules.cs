using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessPriorityControl.Cmd
{
    class ProcessWithRules
    {
        public string Hash { get; }

        public string ShortName { get; }

        public string FullPath { get; }

        public List<string> UserSids { get; }

        public List<string> ParentProcessShortNames { get; }

        public ProcessWithRules(string hash, string shortName, string fullPath, List<string> userSids, List<string> parentProcessShortNames)
        {
            Hash = hash;
            ShortName = shortName;
            FullPath = fullPath;
            UserSids = userSids;
            ParentProcessShortNames = parentProcessShortNames;
        }

        public Priority? GetPriority()
        {
            // Look for full path rule.
            Priority? fullPathPriority = RegistryAccess.GetFullPathRule(this);
            if (fullPathPriority != null)
            {
                return fullPathPriority;
            }

            // Look for short name rule.
            Priority? shortNamePriority = RegistryAccess.GetShortNameRule(this);
            if (shortNamePriority != null)
            {
                return shortNamePriority;
            }

            // Look for partial match rules.
            Priority? partialPriority = RegistryAccess.GetPartialRule(this);
            if (partialPriority != null)
            {
                return partialPriority;
            }

            // Look for username rule.
            if (UserSids.Count == 1)
            {
                Priority? usernamePriority = RegistryAccess.GetUsernameRule(UserSids[0]);
                if (usernamePriority != null)
                {
                    return usernamePriority;
                }
            }

            return null;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();

            // Hash.
            result.Append(Hash);
            result.Append("\n");

            // Short name.
            if (ShortName != string.Empty)
            {
                result.Append("  Short name: " + ShortName);
                result.Append("\n");
            }

            // Full path.
            if (FullPath != string.Empty)
            {
                result.Append("  Full path:  " + FullPath);
                result.Append("\n");
            }

            // Usernames.
            if (UserSids.Count != 0)
            {
                result.Append("  Recorded users:\n");
                foreach (string userSid in UserSids)
                {
                    result.Append("    " + RegistryAccess.GetUsernameFromSid(userSid) + "\n");
                }
            }

            // Parent processes.
            if (ParentProcessShortNames.Count != 0)
            {
                result.Append("  Recorded parent processes:\n");
                foreach (string parent in ParentProcessShortNames)
                {
                    result.Append("    " + parent + "\n");
                }
            }

            return result.ToString();
        }
    }
}
