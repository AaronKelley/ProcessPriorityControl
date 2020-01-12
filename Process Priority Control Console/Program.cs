using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// Class that handles starting the program and core execution.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Tracks active processes; used to tell if a running process has been dealt with already.
        /// </summary>
        private static Dictionary<int, Process> activeProcesses;

        /// <summary>
        /// Tracks active processes; used to determine whether or not any processes have vanished since the last check.
        /// </summary>
        private static HashSet<int> processTrackingHelper;

        /// <summary>
        /// Tracks processes that need the high-power script in place.
        /// </summary>
        private static HashSet<int> highPowerProcesses;

        /// <summary>
        /// Tracks processes that are in the "conditional idle" set.
        /// </summary>
        private static HashSet<int> conditionalIdleProcesses;

        /// <summary>
        /// Path to low-power script.
        /// </summary>
        private static string LowPowerScriptPath;

        /// <summary>
        /// Path to high-power script.
        /// </summary>
        private static string HighPowerScriptPath;

        /// <summary>
        /// During a refresh, used to track whether high-power mode was previously active.
        /// </summary>
        private static bool HighPowerModeActive;

        /// <summary>
        /// Used to determine whether or not power scripts are in use.
        /// </summary>
        private static bool UsingPowerScripts;


        /// <summary>
        /// Main program execution.
        /// </summary>
        /// <param name="args">Command-line parameters</param>
        static void Main(string[] args)
        {
            // Set up data structures.
            activeProcesses = new Dictionary<int, Process>();
            highPowerProcesses = new HashSet<int>();
            conditionalIdleProcesses = new HashSet<int>();

            // Set up the registry structure.
            RegistryAccess.RegistrySetup();

            //ConfigurationMode();

            if (args.Length >= 1 && args[0] == "config")
            {
                // Configuration mode.
                Console.WriteLine("Configuration mode");
                ConfigurationMode();
            }
            else
            {
                // Runtime mode.

                RegistryAccess.ClearChangesMade();

                bool first = true;
                CheckPowerScripts();
                HighPowerModeActive = false;

                while (true)
                {
                    // Repeat... forever.
                    Thread.Sleep(500);

                    if (RegistryAccess.GetChangesMade())
                    {
                        Console.WriteLine("Changes detected from configuration mode, resetting...");
                        activeProcesses.Clear();
                        highPowerProcesses.Clear();
                        conditionalIdleProcesses.Clear();
                        RegistryAccess.ClearChangesMade();
                        first = true;
                        CheckPowerScripts();
                    }

                    Process[] processes = Process.GetProcesses();
                    processTrackingHelper = new HashSet<int>(activeProcesses.Keys);

                    // Confirm that high-power processes are still running.
                    foreach (int processId in highPowerProcesses)
                    {
                        if (!processTrackingHelper.Contains(processId))
                        {
                            highPowerProcesses.Remove(processId);
                            if (highPowerProcesses.Count == 0)
                            {
                                LowPowerMode();
                            }
                        }
                    }

                    // Loop through all running processes.
                    foreach (Process process in processes)
                    {
                        if (!activeProcesses.ContainsKey(process.Id) || activeProcesses[process.Id].ProcessName != process.ProcessName)
                        {
                            // New process.
                            ProcessStarted(process);
                        }
                        else
                        {
                            // Existing process.
                            processTrackingHelper.Remove(process.Id);

                            // Conditional idle priority.  Set idle only if priority is normal.
                            try
                            {
                                if (conditionalIdleProcesses.Contains(process.Id))
                                {
                                    if (process.PriorityClass == ProcessPriorityClass.Normal)
                                    {
                                        Console.WriteLine("  Resetting priority for process {0} to idle", process.Id);
                                        process.PriorityClass = ProcessPriorityClass.Idle;
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                Console.WriteLine("  Unable to handle conditional idle priority: {0}", exception.Message);
                            }
                        }
                    }

                    if (first)
                    {
                        Console.WriteLine("Done enumerating processes that were already running.");
                        first = false;

                        if (highPowerProcesses.Count == 0)
                        {
                            LowPowerMode();
                        }
                    }

                    // The IDs leftover in the tracking helper set are processes that have terminated.
                    foreach (int processId in processTrackingHelper)
                    {
                        ProcessEnded(activeProcesses[processId]);
                    }
                }
            }
        }

        /// <summary>
        /// Text-based configuration mode.
        /// </summary>
        private static void ConfigurationMode()
        {
            bool changesMade = false;
            CheckPowerScripts();

            // Loop through all processes that have been observed.
            foreach (ProcessWithRules process in RegistryAccess.GetObservedProcesses())
            {
                Console.WriteLine(process);

                Priority? priority = process.GetPriority();

                if (priority != null)
                {
                    Console.WriteLine("Priority assigned: " + priority);
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No priority determined for this process.");

                    priority = PriorityPrompt();

                    if (priority != null)
                    {
                        changesMade = true;

                        string prioritySpecify = null;
                        do
                        {
                            Console.WriteLine("How would you like to specify priority?");
                            Console.Write("(F)ull path, (S)hort name, (P)artial match, (U)sername > ");
                            string input = Console.ReadLine().ToLower();
                            if (input == "f" || input == "s" || input == "p" || input == "u")
                            {
                                prioritySpecify = input;
                            }
                        } while (prioritySpecify == null);

                        switch (prioritySpecify)
                        {
                            case "f": // Full path.
                                RegistryAccess.SetFullPathRule(process, (Priority)priority);
                                break;
                            case "s": // Short name.
                                RegistryAccess.SetShortNameRule(process, (Priority)priority);
                                break;
                            case "p": // Partial match.
                                Console.Write("Enter partial path > ");
                                RegistryAccess.SetPartialRule(Console.ReadLine(), (Priority)priority);
                                break;
                            case "u": // Username.
                                RegistryAccess.SetUsernameRule(process, (Priority)priority);
                                break;
                        }
                    }
                }

                Console.WriteLine();
            }

            // Loop through all services that have been observed.
            Console.WriteLine("Checking services...");
            foreach (string serviceName in RegistryAccess.GetObservedServiceNames())
            {
                Console.WriteLine("  {0}", serviceName);

                Priority? priority = RegistryAccess.GetPriorityForService(serviceName);
                if (priority == null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Need to set priority for service \"{0}\"", serviceName);

                    Priority? servicePriority = PriorityPrompt(false);

                    if (servicePriority != null)
                    {
                        changesMade = true;
                        RegistryAccess.SetPriorityForService(serviceName, (Priority)servicePriority);
                    }

                    Console.WriteLine();
                }
            }

            if (changesMade)
            {
                RegistryAccess.SetChangesMade();
            }
        }

        /// <summary>
        /// Prompts the user for the priority and returns what was selected.
        /// </summary>
        /// <param name="allowPowerScripts">If false, power scripts will not be allowed</param>
        /// <returns>Selected priority; NULL if none</returns>
        private static Priority? PriorityPrompt(bool allowPowerScripts = true)
        {
            bool includePowerScriptChoice = UsingPowerScripts && allowPowerScripts;
            Priority? priority = null;

            string priorityChoice = null;
            do
            {
                Console.WriteLine("Which priority would you like to assign?");

                string choices = "(I)dle, (B)elow normal, (N)ormal, (A)bove normal, (H)igh, ";
                if (includePowerScriptChoice)
                {
                    choices += "High with high-(p)ower script, ";
                }
                choices += "(D)efault/Ignore, (S)kip > ";

                Console.Write(choices);
                string input = Console.ReadLine().ToLower();
                if (input == "i" || input == "b" || input == "n" || input == "a" || input == "h" || input == "d" || input == "s" || (includePowerScriptChoice && input == "p"))
                {
                    priorityChoice = input;
                }
            } while (priorityChoice == null);

            if (priorityChoice != "s")
            {
                switch (priorityChoice)
                {
                    case "i":
                        priority = Priority.Idle;
                        break;
                    case "b":
                        priority = Priority.BelowNormal;
                        break;
                    case "n":
                        priority = Priority.Normal;
                        break;
                    case "a":
                        priority = Priority.AboveNormal;
                        break;
                    case "h":
                        priority = Priority.High;
                        break;
                    case "p":
                        priority = Priority.HighWithScript;
                        break;
                    case "d":
                        priority = Priority.Ignore;
                        break;
                }
            }

            return priority;
        }

        /// <summary>
        /// Deal with a process; record it, check the rules, and set the priority.
        /// </summary>
        /// <param name="process">A Windows process to deal with</param>
        /// <param name="parentProcessId">Optional ID of the parent process</param>
        private static void ProcessStarted(Process process, object parentProcessId = null)
        {
            if (activeProcesses.ContainsKey(process.Id))
            {
                // Somehow, a new process started with the same ID as an old one; the swap must have happened between check intervals.
                ProcessEnded(activeProcesses[process.Id]);
            }

            Console.WriteLine("[{0}] Process started: {1} {2}", DateTime.Now, process.Id, process.ProcessName);
            activeProcesses[process.Id] = process;

            try
            {
                ProcessInformation information = new ProcessInformation(process, parentProcessId);

                PrintProcessInformation(information);
                information.RecordProcessInformation();
                AssignProcessPriority(information);
                RunLaunchScript(information);
            }
            catch (Exception exception)
            {
                Console.WriteLine("  [{0}] Unable to handle process {1}: {2}", exception.GetType().ToString(), process.Id, exception.Message);

                if (exception.Message.Contains("Only part of a ReadProcessMemory or WriteProcessMemory request was completed") || exception.Message.Contains("Object reference not set to an instance of an object"))
                {
                    // Sometimes this exception happens when a process has just started, or a process quickly starts and then terminates.
                    // Set up to try again on the next iteration.
                    Console.WriteLine("  Will retry process {0}", process.Id);
                    activeProcesses.Remove(process.Id);
                }
            }
        }

        /// <summary>
        /// Clean-up after a process is detected as terminated.
        /// </summary>
        /// <param name="process">Process that has ended</param>
        private static void ProcessEnded(Process process)
        {
            Console.WriteLine("[{0}] Process ended: {1} {2}", DateTime.Now, process.Id, process.ProcessName);
            activeProcesses.Remove(process.Id);

            if (highPowerProcesses.Contains(process.Id))
            {
                // This was a high-power script process.
                highPowerProcesses.Remove(process.Id);
                if (highPowerProcesses.Count == 0)
                {
                    LowPowerMode();
                }
            }

            if (conditionalIdleProcesses.Contains(process.Id))
            {
                conditionalIdleProcesses.Remove(process.Id);
            }
        }

        /// <summary>
        /// Print some process information to the console.
        /// </summary>
        /// <param name="information">Process information to print</param>
        private static void PrintProcessInformation(ProcessInformation information)
        {
            try
            {
                Console.WriteLine("  {0}", information.FullPath);
                Console.WriteLine(@"  {0}: {1}\{2}", information.User?.Sid, information.User?.Domain, information.User?.Username);

                if (information.ServiceNames != null)
                {
                    Console.Write("  Services:");
                    foreach (string serviceName in information.ServiceNames)
                    {
                        Console.Write(" {0}", serviceName);
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Unable to fetch details: {0}", exception.Message);
            }
        }

        /// <summary>
        /// Use the previously defined priority rules to assign priority for this process.
        /// </summary>
        /// <param name="information">Information object for process to assign priority for</param>
        private static void AssignProcessPriority(ProcessInformation information)
        {
            try
            {
                ProcessWithRules processWithRules = information.GetRulesObject();
                Priority? priority = processWithRules.GetPriority();

                if (information.ServiceNames != null)
                {
                    // Service priority can override process priority.  We will take the highest priority specified.
                    foreach (string serviceName in information.ServiceNames)
                    {
                        Priority? servicePriority = RegistryAccess.GetPriorityForService(serviceName);
                        if (servicePriority != null && servicePriority > priority)
                        {
                            Console.WriteLine("  !! Service priority override");
                            priority = (Priority)servicePriority;
                        }
                    }
                }

                if (priority != null)
                {
                    Process process = Process.GetProcessById(information.ProcessId);
                    if (process != null)
                    {
                        switch (priority)
                        {
                            case Priority.Idle:
                                Console.WriteLine("  Priority set to idle");
                                process.PriorityClass = ProcessPriorityClass.Idle;
                                break;
                            case Priority.ConditionalIdle:
                                Console.WriteLine("  Priority set to conditional idle - idle only if it is already normal");
                                conditionalIdleProcesses.Add(information.ProcessId);
                                if (process.PriorityClass == ProcessPriorityClass.Normal)
                                {
                                    process.PriorityClass = ProcessPriorityClass.Idle;
                                }
                                break;
                            case Priority.BelowNormal:
                                Console.WriteLine("  Priority set to below normal");
                                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                                break;
                            case Priority.Normal:
                                Console.WriteLine("  Priority set to normal");
                                process.PriorityClass = ProcessPriorityClass.Normal;
                                break;
                            case Priority.AboveNormal:
                                Console.WriteLine("  Priority set to above normal");
                                process.PriorityClass = ProcessPriorityClass.AboveNormal;
                                break;
                            case Priority.High:
                                Console.WriteLine("  Priority set to high");
                                process.PriorityClass = ProcessPriorityClass.High;
                                break;
                            case Priority.HighWithScript:
                                Console.WriteLine("  Priority set to high");
                                process.PriorityClass = ProcessPriorityClass.High;
                                highPowerProcesses.Add(information.ProcessId);
                                HighPowerMode();
                                break;
                            case Priority.Ignore:
                                Console.WriteLine("  Not setting priority for this process.");
                                break;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  No priority rule match for this process");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Unable to set priority: {0}", exception.Message);
            }
        }

        private static void RunLaunchScript(ProcessInformation information)
        {
            try
            {
                string launchScript = RegistryAccess.GetLaunchScript(information);
                if (launchScript != null)
                {
                    string[] launchScriptPieces = launchScript.Split(new char[] { ' ' });
                    if (launchScriptPieces.Length == 1)
                    {
                        Console.WriteLine("  Running launch script.");
                        Console.WriteLine("    Process path: {0}", launchScript);
                        Process.Start(launchScript);
                    }
                    else if (launchScriptPieces.Length > 1)
                    {
                        string arguments = launchScript.Substring(launchScriptPieces[0].Length + 1);

                        Console.WriteLine("  Running launch script.");
                        Console.WriteLine("    Process path: {0}", launchScriptPieces[0]);
                        Console.WriteLine("    Arguments:    {0}", arguments);
                        Process.Start(launchScriptPieces[0], arguments);
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Error running launch script: [{0}] {1}", exception.GetType().ToString(), exception.Message);
            }
        }

        /// <summary>
        /// Check the registry and fill in information about power scripts.
        /// </summary>
        private static void CheckPowerScripts()
        {
            LowPowerScriptPath = RegistryAccess.GetLowPowerScript();
            HighPowerScriptPath = RegistryAccess.GetHighPowerScript();
            if (LowPowerScriptPath != null && HighPowerScriptPath != null)
            {
                UsingPowerScripts = true;
                Console.WriteLine("Power scripts are configured.");
            }
            else
            {
                Console.WriteLine("Power scripts are not configured.");
            }
        }

        /// <summary>
        /// Run the high-power script.
        /// </summary>
        private static void HighPowerMode()
        {
            if (UsingPowerScripts && !HighPowerModeActive)
            {
                Console.WriteLine("  Running high-power script");
                Process.Start(HighPowerScriptPath);
                HighPowerModeActive = true;
            }
        }

        /// <summary>
        /// Run the low-power script.
        /// </summary>
        private static void LowPowerMode()
        {
            if (UsingPowerScripts && HighPowerModeActive)
            {
                Console.WriteLine("  Running low-power script");
                Process.Start(LowPowerScriptPath);
                HighPowerModeActive = false;
            }
        }
    }
}
