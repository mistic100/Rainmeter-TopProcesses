using Rainmeter;
using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PluginTopProcesses
{
    class DataThread
    {
        // Rainmeter API
        internal IntPtr rm;
        // Global Query
        internal string QueryString;
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

            this.QueryString = "SELECT * FROM Win32_PerfRawData_PerfProc_Process";

            // Apply globally ignored processes to query
            if (!string.IsNullOrEmpty(GlobalIgnoredProcesses))
            {
                bool first = true;
                foreach (string procName in GlobalIgnoredProcesses.Split(new char[] { '|' }))
                {
                    if (first)
                    {
                        this.QueryString += " WHERE";
                        first = false;
                    }
                    else
                    {
                        this.QueryString += " AND";
                    }
                    this.QueryString += " NOT Name LIKE '" + procName.Replace("*", "%").Replace("'", "''") + "'";
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

        void DoQuery()
        {
            // Get list of processes
            List<Performance> iterationList = new List<Performance>();
            try
            {
                using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(this.QueryString))
                using (ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get())
                using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementObjectCollection.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        ManagementObject managementObject = (ManagementObject)enumerator.Current;
                        Performance performance = this.PerfList.Find((Performance p) => p.Equals(managementObject));
                        if (performance == null)
                        {
                            performance = new Performance(managementObject);
                            this.PerfList.Add(performance);
                        }
                        else
                        {
                            performance.Update(managementObject);
                        }
                        iterationList.Add(performance);
                    }
                }
            }
            catch(ManagementException e)
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
