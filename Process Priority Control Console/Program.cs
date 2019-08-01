using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;

namespace ProcessPriorityControl.Cmd
{
    /// <summary>
    /// Class that handles starting the program and core execution.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Tracks active processes.
        /// </summary>
        private static Dictionary<int, Process> activeProcesses;

        private static HashSet<int> processTrackingHelper;

        /// <summary>
        /// Main program execution.
        /// </summary>
        /// <param name="args">Command-line parameters</param>
        static void Main(string[] args)
        {
            // Set up data structure.
            activeProcesses = new Dictionary<int, Process>();

            // Set up the registry structure.
            RegistryAccess.RegistrySetup();

            if (args.Length >= 1 && args[0] == "config")
            {
                // Configuration mode.
                Console.WriteLine("Configuration mode");
                ConfigurationMode();
            }
            else
            {
                // Runtime mode.

                //ListenForProcesses();

                bool first = true;
                while (true)
                {
                    // Repeat... forever.
                    Thread.Sleep(500);

                    Process[] processes = Process.GetProcesses();
                    processTrackingHelper = new HashSet<int>(activeProcesses.Keys);
                    foreach (Process process in processes)
                    {
                        if (!activeProcesses.ContainsKey(process.Id))
                        {
                            // New process.
                            DealWithProcess(process);
                        }
                        else
                        {
                            // Existing process.
                            processTrackingHelper.Remove(process.Id);
                        }
                    }

                    if (first)
                    {
                        Console.WriteLine("Done enumerating processes that were already running.");
                        first = false;
                    }

                    // The IDs leftover in the tracking helper set are processes that have terminated.
                    foreach (int processId in processTrackingHelper)
                    {
                        Process process = activeProcesses[processId];
                        Console.WriteLine("[{0}] Process ended: {1} {2}", DateTime.Now, processId, process.ProcessName);
                        activeProcesses.Remove(processId);
                    }
                }
            }
        }

        /// <summary>
        /// Text-based configuration mode.
        /// </summary>
        private static void ConfigurationMode()
        {
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

                    string priorityChoice = null;
                    do
                    {
                        Console.WriteLine("Which priority would you like to assign?");
                        Console.Write("(I)dle, (B)elow normal, (N)ormal, (A)bove normal, (H)igh, (D)efault/Ignore, (S)kip > ");
                        string input = Console.ReadLine().ToLower();
                        if (input == "i" || input == "b" || input == "n" || input == "a" || input == "h" || input == "d" || input == "s")
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
                            case "d":
                                priority = Priority.Ignore;
                                break;
                        }

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

        }

        /// <summary>
        /// Deal with a process; record it, check the rules, and set the priority.
        /// </summary>
        /// <param name="process">A Windows process to deal with</param>
        /// <param name="parentProcessId">Optional ID of the parent process</param>
        private static void DealWithProcess(Process process, object parentProcessId = null)
        {
            Console.WriteLine("[{0}] Process started: {1} {2}", DateTime.Now, process.Id, process.ProcessName);
            activeProcesses[process.Id] = process;

            try
            {
                ProcessInformation information = new ProcessInformation(process, parentProcessId);

                PrintProcessInformation(information);
                information.RecordProcessInformation();
                AssignProcessPriority(information);
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Unable to handle process {0}: {1}", process.Id, exception.Message);
            }
        }

        /// <summary>
        /// Deal with a process; record it, check the rules, and set the priority.
        /// </summary>
        /// <param name="processId">ID of a Windows process to deal with</param>
        /// <param name="parentProcessId">ID of the parent process</param>
        private static void DealWithProcess(object processId, object parentProcessId)
        {
            try
            {
                DealWithProcess(Process.GetProcessById(int.Parse(processId.ToString())));
            }
            catch (Exception exception)
            {
                Console.WriteLine("  Unable to handle process {0}: {1}", processId.ToString(), exception.Message);
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

        /// <summary>
        /// Start listening for process start and stop events.
        /// </summary>
        /// <seealso cref="https://stackoverflow.com/questions/967646/monitor-when-an-exe-is-launched/967668#967668"/>
        private static void ListenForProcesses()
        {
            ManagementEventWatcher processStartWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            ManagementEventWatcher processStopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));

            processStartWatcher.EventArrived += new EventArrivedEventHandler(ProcessStartedEventHandler);
            processStopWatcher.EventArrived += new EventArrivedEventHandler(ProcessStoppedEventHandler);

            processStartWatcher.Start();
            processStopWatcher.Start();
        }

        /// <summary>
        /// Event handler fired when a new process is started.
        /// </summary>
        /// <param name="sender">Event originator</param>
        /// <param name="arguments">Event information</param>
        public static void ProcessStartedEventHandler(object sender, EventArrivedEventArgs arguments)
        {
            Console.WriteLine("Process started: {0} {1}", arguments.NewEvent.Properties["ProcessID"].Value, arguments.NewEvent.Properties["ProcessName"].Value);
            DealWithProcess(arguments.NewEvent.Properties["ProcessID"].Value, arguments.NewEvent.Properties["ParentProcessID"].Value);

            /*
             * Available properties:
             *
             * ParentProcessID
             * ProcessID
             * ProcessName
             * SessionID
             * Sid (bytes)
             * TIME_CREATED
             * SECURITY_DESCRIPTOR
             */
        }

        /// <summary>
        /// Event handler fired when a process terminates.
        /// </summary>
        /// <param name="sender">Event originator</param>
        /// <param name="arguments">Event information</param>
        public static void ProcessStoppedEventHandler(object sender, EventArrivedEventArgs arguments)
        {
            Console.WriteLine("Process stopped: {0} {1}", arguments.NewEvent.Properties["ProcessID"].Value, arguments.NewEvent.Properties["ProcessName"].Value);
        }
    }
}
