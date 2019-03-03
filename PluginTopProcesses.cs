using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using Rainmeter;

namespace PluginTopProcesses
{
    internal class Measure
    {
        // Top CPU usage or Top Memory usage
        internal enum MetricType
        {
            TopCPU,
            TopMemory
        }

        // Rainmeter API
        internal Rainmeter.API api;
        internal IntPtr rm;
        // Measure Name
        internal string Name;
        // Skin Pointer
        internal IntPtr Skin;
        // Type
        internal MetricType Type = MetricType.TopCPU;
        // Perform data query
        private bool ReQuery = false;
        // Ignored processes per measure
        internal string SpecificIgnoredProcesses;
        // Output Format
        internal string Format;
        // Start Process
        internal int StartProcNum = 0;
        // End Process
        internal int EndProcNum = 5;

        // Global Query
        internal string QueryString;
        // Sorted lists of processes
        internal List<Performance> _cpuList;
        internal List<Performance> _memList;
        // Pointer of measure with ReQuery = true
        private IntPtr DataProvider;
        // Found DataProvider, if false, no measure with ReQuery = true
        private bool HasData = false;
        // Last data output
        private string lastData = string.Empty;

        public Measure(IntPtr rm)
        {
            this.rm = rm;
            this.api = new Rainmeter.API(rm);
        }

        internal void Reload()
        {
            // Pointers / name
            this.Name = api.GetMeasureName();
            this.Skin = api.GetSkin();

            // Measure does the data refresh
            string reQuery = api.ReadString("ReQuery", string.Empty);
            if (reQuery.Equals("1") || reQuery.Equals("true", StringComparison.InvariantCultureIgnoreCase))
            {
                this.ReQuery = true;
            }

            // Metric Type
            string type = api.ReadString("MetricType", string.Empty);
            switch (type.ToLowerInvariant())
            {
                case "memory":
                case "mem":
                    this.Type = MetricType.TopMemory;
                    break;

                default:
                    this.Type = MetricType.TopCPU;
                    break;
            }

            // If provides data
            if (this.ReQuery)
            {
                this.QueryString = "SELECT * FROM Win32_PerfRawData_PerfProc_Process";

                // Apply globally ignored processes to query
                string globalIgnoredProcesses = api.ReadString("GlobalIgnoredProcesses", string.Empty);
                if (!string.IsNullOrEmpty(globalIgnoredProcesses))
                {
                    bool first = true;
                    foreach (string procName in globalIgnoredProcesses.Split(new char[] { '|' }))
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

                // Create lists only if data provider
                this._cpuList = new List<Performance>();
                this._memList = new List<Performance>();
            }

            // Find the measure in this skin with ReQuery=1
            foreach (KeyValuePair<IntPtr, Measure> current in Plugin.Measures)
            {
                Measure value = current.Value;
                if (value.Skin.Equals(this.Skin) && value.ReQuery)
                {
                    this.DataProvider = current.Key;
                    this.HasData = true;
                    break;
                }
            }

            // Set ignored processes for this measure
            this.SpecificIgnoredProcesses = api.ReadString("SpecificIgnoredProcesses", string.Empty);

            // Set format for this measure
            this.Format = api.ReadString("Format", string.Empty);

            // Set range or specific process number to display
            string procNum = api.ReadString("ProcNums", string.Empty);
            if (!string.IsNullOrEmpty(procNum))
            {
                string[] procNums = procNum.Split(new char[] { '-' });
                if (procNums.Length == 1)
                {
                    this.StartProcNum = Convert.ToInt32(procNums[0]);
                    this.EndProcNum = Convert.ToInt32(procNums[0]);
                }
                else if (procNums.Length == 2)
                {
                    this.StartProcNum = Convert.ToInt32(procNums[0]);
                    this.EndProcNum = Convert.ToInt32(procNums[1]);
                }
            }
        }

        internal void RefreshData()
        {
            lock (this._cpuList)
            {
                // Get list of processes
                List<Performance> iterationList = new List<Performance>();
                using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(this.QueryString))
                using (ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get())
                using (ManagementObjectCollection.ManagementObjectEnumerator enumerator = managementObjectCollection.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        ManagementObject managementObject = (ManagementObject)enumerator.Current;
                        Performance performance = this._cpuList.Find((Performance p) => p.Equals(managementObject));
                        if (performance == null)
                        {
                            performance = new Performance(managementObject);
                            this._cpuList.Add(performance);
                        }
                        else
                        {
                            performance.Update(managementObject);
                        }
                        iterationList.Add(performance);
                    }
                }

                // Remove any processes that have been killed
                foreach (Performance current in this._cpuList.FindAll((Performance p) => !iterationList.Contains(p)))
                {
                    this._cpuList.Remove(current);
                }

                // Copy to memory list
                this._memList.Clear();
                this._memList.AddRange(this._cpuList);

                // Sort by CPU usage and by memory
                this._cpuList.Sort((Performance p1, Performance p2) => p2.PercentProc.CompareTo(p1.PercentProc));
                this._memList.Sort((Performance p1, Performance p2) => p2.CurrentMemory.CompareTo(p1.CurrentMemory));
            }
        }

        internal double Update()
        {
            // Do a refresh if ReQuery = 1
            if (this.ReQuery)
            {
                this.RefreshData();
            }

            // If found a measure with ReQuery = 1
            if (this.HasData)
            {
                Measure measure = Plugin.Measures[this.DataProvider];
                List<Performance> cpuList = measure._cpuList;
                List<Performance> memList = measure._memList;

                // Return numeric data if only one raw data
                if (this.Format.Equals(Performance.FORMAT_CPU_RAW) && this.StartProcNum.Equals(this.EndProcNum))
                {
                    return cpuList[this.StartProcNum].PercentProc * 100;
                }
                if (this.Format.Equals(Performance.FORMAT_MEMORY_RAW) && this.StartProcNum.Equals(this.EndProcNum))
                {
                    return memList[this.StartProcNum].CurrentMemory;
                }

                // Or build the output string
                string returnString = string.Empty;

                // Ensure in the bounds of the processes array
                int startProc = this.StartProcNum;
                int endProc = this.EndProcNum;

                if (startProc >= cpuList.Count)
                {
                    startProc = 0;
                }
                if (endProc >= cpuList.Count)
                {
                    endProc = cpuList.Count - 1;
                }

                // Output process list
                for (int i = startProc; i <= endProc; i++)
                {
                    bool ignoredProcess = false;

                    if (!string.IsNullOrEmpty(this.SpecificIgnoredProcesses))
                    {
                        string[] ignored = this.SpecificIgnoredProcesses.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                        string procName = string.Empty;
                        if (this.Type.Equals(MetricType.TopMemory))
                        {
                            procName = memList[i].Name;
                        }
                        else
                        {
                            procName = cpuList[i].Name;
                        }

                        // Determine if process is ignored
                        foreach (string current in ignored)
                        {
                            if (Utils.WildcardMatch(procName, current))
                            {
                                // Adjust looping for ignored process
                                ignoredProcess = true;
                                endProc++;
                                if (endProc >= cpuList.Count)
                                {
                                    endProc = cpuList.Count - 1;
                                }
                                break;
                            }
                        }
                    }

                    // If not ignored, add it to the output
                    if (!ignoredProcess)
                    {
                        if (this.Type.Equals(MetricType.TopMemory))
                        {
                            returnString += memList[i].ToString(this.Format);
                        }
                        else
                        {
                            returnString += cpuList[i].ToString(this.Format);
                        }

                        returnString += Environment.NewLine;
                    }
                }

                // Trim off any newlines & store
                this.lastData = returnString.TrimEnd();
            }

            return 0.0;
        }

        internal string GetString()
        {
            if (this.StartProcNum.Equals(this.EndProcNum) && (this.Format.Equals(Performance.FORMAT_CPU_RAW) || this.Format.Equals(Performance.FORMAT_MEMORY_RAW)))
            {
                return null;
            }

            return this.lastData;
        }
    }

    public static class Plugin
    {
        internal static Dictionary<IntPtr, Measure> Measures = new Dictionary<IntPtr, Measure>();

        [DllExport]
        public unsafe static void Initialize(ref IntPtr data, IntPtr rm)
        {
            Plugin.Measures.Add(data, new Measure(rm));
        }

        [DllExport]
        public unsafe static void Finalize(IntPtr data)
        {
            Plugin.Measures.Remove(data);
        }

        [DllExport]
        public unsafe static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = Plugin.Measures[data];
            try
            {
                measure.Reload();
            }
            catch (Exception e)
            {
                API.LogF(measure.rm, API.LogType.Error, "Reload error {0}", e.Message);
            }
        }

        [DllExport]
        public unsafe static double Update(IntPtr data)
        {
            Measure measure = Plugin.Measures[data];
            try
            {
                return measure.Update();
            }
            catch (Exception e)
            {
                API.LogF(measure.rm, API.LogType.Error, "Update error {0}", e.Message);
                return 0.0;
            }
        }

        [DllExport]
        public unsafe static IntPtr GetString(IntPtr data)
        {
            string value = Plugin.Measures[data].GetString();

            if (value != null)
            {
                return Marshal.StringToHGlobalAuto(value);
            }
            else
            {
                return IntPtr.Zero;
            }
        }
    }
}
