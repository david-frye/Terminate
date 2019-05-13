/*
 * Copyright (c) 2019, Lawrence Livermore National Security, LLC.
 * Produced at the Lawrence Livermore National Laboratory.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Win32;
using System.Management;
using System.IO;
using gov.llnl.logging;
using System.Reflection;

namespace gov.llnl.terminate
{
    class Program
    {
        public enum ContractEnum { Tag, Kill }

        public class Target
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public int PID { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime DiscoveryTime { get; set; }
            public int TargetAge
            {
                get
                {
                    int targetAge = 0;
                    TimeSpan lifeSpan = DiscoveryTime - StartTime;
                    targetAge += lifeSpan.Days * 24 * 60 + lifeSpan.Hours * 60 + lifeSpan.Minutes;
                    return targetAge;
                }
            }
        }

        static void Main(string[] args)
        {
            // diagnostics log, written to %TEMP% by default
            Logit log = new Logit();
            log.Verbosity = LogVerboseLevel.Normal;
            if(args.Contains("LOG=DEBUG"))
            {
                log.Verbosity = LogVerboseLevel.Debug;
            }
            log.Init();
            string targetName = "NA";
            int maxTTL = 0;
            ContractEnum contractType = ContractEnum.Tag;
            int targetsProcessed = 0;
            bool proceed = true;

            try
            {
                targetName = pullTargetFromArgs(args, log);
                maxTTL = pullTTLFromArgs(args, log);
                contractType = pullContractFromArgs(args, log);
                if (targetName == "NA" || maxTTL == 0)
                {
                    displayHelp();
                    proceed = false;
                }
            }
            catch (Exception ex)
            {
                log.Append("Error pulling command line parameters: " + ex.Message, LogVerboseLevel.Normal);
                proceed = false;
            }
            if (proceed)
            {
                log.Append("Terminate is starting", LogVerboseLevel.Normal);
                log.Append("     target app: " + targetName, LogVerboseLevel.Normal);
                log.Append("     max time to live (minutes): " + maxTTL, LogVerboseLevel.Normal);
                log.Append("     contract type: " + contractType, LogVerboseLevel.Normal);
                targetsProcessed = runTermination(log, targetName, contractType, maxTTL, targetsProcessed);
                log.Append("Total targets processed: " + targetsProcessed, LogVerboseLevel.Normal);
                log.Append("Terminate is complete.   Shutting down.", LogVerboseLevel.Normal);
            }
            quit(log);
        }

        static int runTermination(Logit log, string targetName, ContractEnum contractType, int maxTTL, int targetsProcessed)
        {
            log.Append("acquiring targets...", LogVerboseLevel.Normal);
            List<Target> targets = new List<Target>();
            try
            {
                targets = acquireTargets(targetName, log);
            }
            catch (Exception ex)
            {
                log.Append("Error acquiring targets: " + ex.Message, LogVerboseLevel.Normal);
            }
            if (targets.Count > 0)
            {
                log.Append("targets acquired: " + targets.Count + "  processing targets...", LogVerboseLevel.Normal);
                foreach (Target target in targets)
                {
                    if (processTarget(target, contractType, maxTTL, log))
                    {
                        targetsProcessed++;
                    }
                }
            }
            return targetsProcessed;
        }

        static bool processTarget(Target target, ContractEnum contract, int ttl, Logit log)
        {
            bool success = false;
            try
            {
                if (contract == ContractEnum.Kill && target.TargetAge > ttl)
                {
                    log.Append("Killing process: " + target.Name, LogVerboseLevel.Normal);
                    Process deadProcRunning = Process.GetProcessById(target.PID);
                    deadProcRunning.Kill();
                    log.Append("     done", LogVerboseLevel.Normal);
                    success = true;
                }
                else if (contract == ContractEnum.Tag && target.TargetAge > ttl)
                {
                    log.Append("tagging process: " + target.Name, LogVerboseLevel.Normal);
                    RegistryKey ldKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\LANDesk\\ManagementSuite\\WinClient");
                    string ldPath = ldKey.GetValue("Path").ToString();
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = ldPath + "\\miniscan.exe";
                    psi.Arguments = "\"/send=Custom Data - Support - ProcessName = " + target.Name + "\"";
                    psi.WorkingDirectory = ldPath;
                    Process myProc = Process.Start(psi);
                    myProc.WaitForExit();
                    psi.Arguments = "\"/send=Custom Data - Support - ProcessAgeMinutes = " + target.TargetAge + "\"";
                    myProc = Process.Start(psi);
                    myProc.WaitForExit();
                    log.Append("     done", LogVerboseLevel.Normal);
                    success = true;
                }
                else
                {
                    log.Append("Hit aborted.  target too young: " + target.TargetAge + " minutes, name: " + target.Name, LogVerboseLevel.Normal);
                }
            }
            catch(Exception ex)
            {
                log.Append("Error completing processing target: " + target.Name + "  error: " + ex.Message, LogVerboseLevel.Normal);
            }         
            return success;
        }

        static List<Target> acquireTargets(string name, Logit log)
        {
            List<Target> targets = new List<Target>();
            List<Target> allprocs = GetActiveProcessList(log);
            foreach (Target proc in allprocs)
            {
                log.Append("evaluating: " + proc.Name, LogVerboseLevel.Debug);
                if (proc.Name.ToLower() == name.ToLower())
                {
                    try
                    {
                        Target target = new Target();
                        target.Name = proc.Name;
                        target.Path = proc.Path;
                        target.StartTime = proc.StartTime;
                        target.DiscoveryTime = DateTime.Now;
                        target.PID = proc.PID;
                        targets.Add(target);
                    }
                    catch(Exception ex)
                    {
                        log.Append("Warning: could not evaluate potential target: " + proc.Name + "  error: " + ex.Message, LogVerboseLevel.Normal);
                    }
                }
            }
            return targets;
        }

        static void quit(Logit log)
        {
            log.Close();
        }

        static void displayHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Terminate, v." + Assembly.GetCallingAssembly().GetName().Version.ToString());
            Console.WriteLine("*****************************");
            Console.WriteLine("Terminate is a diagnostics and recovery tool designed to help LANDesk administrators. Terminate can either report on (tag) or stop (kill) a specified process on LANDesk client computers. If KILL is specified, all processes that match the specified name and have a StartTime older than the specified minutes will be force stopped.  If TAG is specified, a process that matches the name and has an old StartTime will be reported to LANDesk inventory");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("Terminate.exe TARGET=<target-process-name> TTL=<max-process-age-in-minutes> CONTRACT=<TAG-or-KILL>");
            Console.WriteLine("process stop example: ");
            Console.WriteLine("Terminate.exe TARGET=notepad.exe TTL=15 CONTRACT=KILL");
            Console.WriteLine("process report example: ");
            Console.WriteLine("Terminate.exe TARGET=notepad.exe TTL=15 CONTRACT=TAG");
            Console.WriteLine("");
            Console.WriteLine("NOTES:");
            Console.WriteLine("Terminate command parameters are case insensitive.  Process name can be specified with or without the .exe suffix");
            Console.WriteLine("");
            Console.WriteLine("Terminate logs output to the console window (when run interactively) as well as to a log file at %TEMP% which will resolve to Windows\\Temp if run as a LANDesk task");
            Console.WriteLine("");
            Console.WriteLine("Using the 'tag' parameter enables the LANDesk administrator to view process info centrally from the LANDesk console.  However, to use this feature, you must enable custom data.  When ‘tag’ is used, terminate.exe will use miniscan.exe to send custom data from the client to the LANDesk core at these two custom data paths:  ");
            Console.WriteLine("Custom Data - Support - ProcessName");
            Console.WriteLine("and");
            Console.WriteLine("Custom Data - Support - ProcessAgeMinutes");
            Console.WriteLine("");
        }

        static string pullTargetFromArgs(string[] args, Logit log)
        {
            string targetName = "NA";
            foreach (string arg in args)
            {
                if (arg.ToLower().StartsWith("target="))
                {
                    targetName = arg.Split(new char[] { '=' })[1].ToLower();
                    if (!targetName.EndsWith(".exe"))
                    {
                        targetName = targetName + ".exe";
                    }
                    break;
                }
            }
            return targetName;
        }

        static int pullTTLFromArgs(string[] args, Logit log)
        {
            int ttl = 0;
            foreach (string arg in args)
            {
                if (arg.ToLower().StartsWith("ttl="))
                {
                    ttl = Convert.ToInt32(arg.Split(new char[] { '=' })[1]);
                    break;
                }
            }
            return ttl;
        }

        static ContractEnum pullContractFromArgs(string[] args, Logit log)
        {
            ContractEnum hitType = ContractEnum.Tag;
            foreach (string arg in args)
            {
                if (arg.ToLower().StartsWith("contract="))
                {
                    if (arg.Split(new char[] { '=' })[1].ToUpper() == "KILL")
                    {
                        hitType = ContractEnum.Kill;
                        break;
                    }
                }
            }
            return hitType;
        }

        static List<Target> GetActiveProcessList(Logit log)
        {
            List<Target> activeList = new List<Target>();
            WqlObjectQuery w = new WqlObjectQuery("Select * from Win32_Process");
            ManagementObjectSearcher mos = new ManagementObjectSearcher(w);
            foreach (ManagementObject mo in mos.Get())
            {
                try
                {
                    Target po = new Target();
                    po.PID = Convert.ToInt32(mo.Properties["ProcessID"].Value.ToString());
                    try
                    {
                        po.StartTime = convertFromWmiToDotNetDateTime(mo.Properties["CreationDate"].Value.ToString());
                    }
                    catch { }
                    string pathName = "NA";
                    if (po.PID == 0)
                    {
                        pathName = "System Idle Process";
                    }
                    else if (po.PID == 4)
                    {
                        pathName = "System";
                    }
                    else
                    {
                        try
                        {
                            pathName = mo.Properties["ExecutablePath"].Value.ToString();
                        }
                        catch
                        {
                            pathName = mo.Properties["Caption"].Value.ToString();  // can fail when attempting to get extended process info on protected processes, until i do, we use the process name as path
                        }
                    }
                    po.Name = mo.Properties["Caption"].Value.ToString().ToLower();
                    po.Path = pathName.ToLower();
                    activeList.Add(po);
                }
                catch (Exception ex)
                {
                    log.Append("Warning:  could not get details on process: " + mo.Path, LogVerboseLevel.Normal);
                }
            }
            return activeList;
        }

        /// <summary>
        /// converts from WMI time format to .net. 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        static DateTime convertFromWmiToDotNetDateTime(string wmiTime)
        {

            int year = int.Parse(wmiTime.Substring(0, 4));
            int month = int.Parse(wmiTime.Substring(4, 2));
            int day = int.Parse(wmiTime.Substring(6, 2));
            int hour = int.Parse(wmiTime.Substring(8, 2));
            int minute = int.Parse(wmiTime.Substring(10, 2));
            int second = int.Parse(wmiTime.Substring(12, 2));
            int ms = int.Parse(wmiTime.Substring(15, 3));
            DateTime date = new DateTime(year, month, day, hour, minute, second);
            date = date.AddMilliseconds(ms);
            return date;
        }
    }
}
