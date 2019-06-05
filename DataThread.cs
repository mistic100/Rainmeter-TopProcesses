using Rainmeter;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace PluginTopProcesses
{
    class DataThread
    {
        // Rainmeter API
        internal IntPtr rm;
        // Ignored processes
        internal List<Regex> GlobalIgnoredProcesses;
        // De-duplicate multiple processes
        internal bool Dedupe = true;
        // Async database read
        internal bool Async = true;

        // Performance computers
        internal List<Performance> PerfList;
        // Sorted lists of processes
        internal List<Performance.Data> CpuList;
        internal List<Performance.Data> MemList;

        public DataThread(IntPtr rm, bool Async, bool Dedupe, string GlobalIgnoredProcesses)
        {
            this.rm = rm;
            this.Async = Async;
            this.Dedupe = Dedupe;

            this.PerfList = new List<Performance>();
            this.CpuList = new List<Performance.Data>();
            this.MemList = new List<Performance.Data>();
            this.GlobalIgnoredProcesses = new List<Regex>();

            this.GlobalIgnoredProcesses.Add(new Regex("^Idle$"));
            this.GlobalIgnoredProcesses.Add(new Regex("^_Total$"));

            if (!string.IsNullOrEmpty(GlobalIgnoredProcesses))
            {
                foreach (string procName in GlobalIgnoredProcesses.Split(new char[] { '|' }))
                {
                    if (procName.StartsWith("*") && procName.EndsWith("*"))
                    {
                        this.GlobalIgnoredProcesses.Add(new Regex(procName));
                    }
                    else if (procName.StartsWith("*"))
                    {
                        this.GlobalIgnoredProcesses.Add(new Regex(procName + "$"));
                    }
                    else if (procName.EndsWith("*"))
                    {
                        this.GlobalIgnoredProcesses.Add(new Regex("^" + procName));
                    }
                    else
                    {
                        this.GlobalIgnoredProcesses.Add(new Regex("^" + procName + "$"));
                    }
                }
            }
        }

        public void Query()
        {
            if (this.Async)
            {
                API.Log(this.rm, API.LogType.Debug, "TopProcesses: Start query thread");
                new Thread(this.DoQuery).Start();
            }
            else
            {
                this.DoQuery();
            }
        }

        public List<Performance.Data> GetMemList()
        {
            lock (this)
            {
                return new List<Performance.Data>(this.MemList);
            }
        }

        public List<Performance.Data> GetCpuList()
        {
            lock (this)
            {
                return new List<Performance.Data>(this.CpuList);
            }
        }

        private bool IsIgnored(string procName)
        {
            foreach (Regex ignoredName in this.GlobalIgnoredProcesses)
            {
                if (ignoredName.IsMatch(procName))
                {
                    return true;
                }
            }

            return false;
        }

        void DoQuery()
        {
            // Get list of processes
            List<Performance> iterationList = new List<Performance>();
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "cmd";
                proc.StartInfo.Arguments = "/c wmic path Win32_PerfRawData_PerfProc_Process get IDProcess, Name, PercentProcessorTime, TimeStamp_Sys100NS, WorkingSetPrivate";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();

                Regex regex = new Regex(@"([0-9]+) +(.+?) +([0-9]+) +([0-9]+) +([0-9]+)");

                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    Match match = regex.Match(line);
                    if (match.Groups.Count == 6)
                    {
                        int procId = int.Parse(match.Groups[1].Value);
                        string procName = match.Groups[2].Value;
                        Int64 procTime = Int64.Parse(match.Groups[3].Value);
                        Int64 timestamp = Int64.Parse(match.Groups[4].Value);
                        Int64 procMem = Int64.Parse(match.Groups[5].Value);

                        if (!this.IsIgnored(procName))
                        {
                            Performance performance = this.PerfList.Find((Performance p) => p.Name.Equals(procName) && p.ProcessId.Equals(procId));
                            if (performance == null)
                            {
                                performance = new Performance(procId, procName, timestamp, procTime, procMem);
                                this.PerfList.Add(performance);
                            }
                            else
                            {
                                performance.Update(timestamp, procTime, procMem);
                            }

                            iterationList.Add(performance);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                API.LogF(this.rm, API.LogType.Warning, "TopProcesses: {0}", e.Message);
            }

            // Remove any processes that have been killed
            foreach (Performance current in this.PerfList.FindAll((Performance p) => !iterationList.Contains(p)))
            {
                this.PerfList.Remove(current);
            }

            // Convert to data and dedupe
            Dictionary<string, Performance.Data> perfData = new Dictionary<string, Performance.Data>();
            foreach (Performance current in this.PerfList)
            {
                string name = current.Name;
                if (this.Dedupe)
                {
                    name = Regex.Replace(name, @"#[0-9]+$", "");
                }
                if (!perfData.ContainsKey(name))
                {
                    perfData[name] = current.ToData();
                }
                else
                {
                    perfData[name].Add(current.ToData());
                }
            }

            lock (this)
            {
                // Copy to CPU list and sort
                this.CpuList.Clear();
                this.CpuList.AddRange(perfData.Values);
                this.CpuList.Sort((Performance.Data p1, Performance.Data p2) => p2.PercentProc.CompareTo(p1.PercentProc));

                // Copy to memory list and sort
                this.MemList.Clear();
                this.MemList.AddRange(perfData.Values);
                this.MemList.Sort((Performance.Data p1, Performance.Data p2) => p2.Memory.CompareTo(p1.Memory));
            }

            API.Log(this.rm, API.LogType.Debug, "TopProcesses: Querying done");
        }
    }
}
