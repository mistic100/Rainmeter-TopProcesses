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

        // Measure Name
        internal string Name;
        // Skin Pointer
        internal IntPtr Skin;

        // Type
        internal MetricType Type = MetricType.TopCPU;
        // Refresh Delay
        internal int UpdateDivider = 1;
        // Global Query
        internal string QueryString;

        // Current Delay Iteration
        internal int _updateIteration;

        // Sorted lists of processes
        internal List<Performance> _cpuList;
        internal List<Performance> _memList;

        // Perform data query
        private bool ReQuery = false;
        // Pointer of measure with ReQuery = true
        private uint DataProvider;
        // Found DataProvider, if false, no measure with ReQuery = true
        private bool HasData = false;

        // Ignored processes per measure
        internal string[] SpecificIgnoredProcesses;
        // Output Format
        internal string Format;

        // Start Process
        internal int StartProcNum = 0;
        // End Process
        internal int EndProcNum = 5;

        // Last data output
        private string lastData = string.Empty;

        internal void Reload(Rainmeter.API api, ref double maxValue)
        {
            // Pointers / name
            Name = api.GetMeasureName();
            Skin = api.GetSkin();

            // Measure does the data refresh
            string reQuery = api.ReadString("ReQuery", string.Empty);
            if (reQuery.Equals("1") || reQuery.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                ReQuery = true;

            // Metric Type
            string type = api.ReadString("MetricType", string.Empty);
            switch (type.ToLowerInvariant())
            {
                case "cpu":
                    Type = MetricType.TopCPU;
                    break;

                case "memory":
                    Type = MetricType.TopMemory;
                    break;

                case "mem":
                    Type = MetricType.TopMemory;
                    break;

                default:
                    Type = MetricType.TopCPU;
                    break;
            }

            // If provides data
            if (ReQuery)
            {
                // Apply globally ignored proceses to query
                string globalIgnoredProcesses = api.ReadString("GlobalIgnoredProcesses", string.Empty);
                QueryString = "SELECT * FROM Win32_PerfRawData_PerfProc_Process";
                if (!String.IsNullOrEmpty(globalIgnoredProcesses))
                {
                    bool firstTime = true;
                    foreach (string procName in globalIgnoredProcesses.Split(new char[] { '|' }))
                    {
                        if (firstTime)
                        {
                            QueryString += " WHERE";
                            firstTime = false;
                        }
                        else
                        {
                            QueryString += " AND";
                        }
                        QueryString += " NOT Name LIKE '" + procName.Replace("*", "%").Replace("'", "''") + "'";
                    }

                }

                // Create lists only if data provider
                _cpuList = new List<Performance>();
                _memList = new List<Performance>();

                // Update delay
                UpdateDivider = api.ReadInt("UpdateDivider", 1);
                if (UpdateDivider < 1)
                    UpdateDivider = 1;
            }

            // Find the measure in this skin with ReQuery=1
            foreach (KeyValuePair<uint, Measure> pair in Plugin.Measures)
            {
                Measure measure = pair.Value;
                if (measure.Skin.Equals(Skin))
                {
                    if (measure.ReQuery)
                    {
                        DataProvider = pair.Key;
                        HasData = true;
                        break;
                    }
                }
            }

            // Set ignored processes for this measure
            string ignored = api.ReadString("SpecificIgnoredProcesses", string.Empty);
            SpecificIgnoredProcesses = ignored.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                
            // Set format for this measure
            Format = api.ReadString("Format", string.Empty);

            // Set range or specific process number to display
            string procNum = api.ReadString("ProcNums", string.Empty);
            if (!String.IsNullOrEmpty(procNum))
            {
                string[] procNums = procNum.Split(new char[] { '-' });
                if (procNums.Length == 1)
                {
                    int num = Convert.ToInt32(procNums[0]);
                    StartProcNum = num;
                    EndProcNum = num;
                }
                else if (procNums.Length == 2)
                {
                    StartProcNum = Convert.ToInt32(procNums[0]);
                    EndProcNum = Convert.ToInt32(procNums[1]);
                }
            }
        }

        internal void RefreshData()
        {
            // Only Refresh if the data provider
            if (ReQuery)
            {
                lock (_cpuList)
                {
                    // Only run if the update delay has been met
                    bool run = false;
                    if (_updateIteration > 1)
                    {
                        _updateIteration--;
                    }
                    else
                    {
                        _updateIteration = UpdateDivider;
                        run = true;
                    }

                    if (run)
                    {
                        // Get list of processes
                        List<Performance> iterationList = new List<Performance>();
                        ManagementObjectCollection mgmtObjs = (new ManagementObjectSearcher(QueryString)).Get();
                        // Modify existing in list or add new
                        foreach (ManagementObject process in mgmtObjs)
                        {
                            ManagementObject lambdaProc = process;
                            Performance currentPerf = _cpuList.Find(p => p.Equals(lambdaProc));
                            if (currentPerf == null)
                            {
                                currentPerf = new Performance(process);
                                _cpuList.Add(currentPerf);
                            }
                            else
                            {
                                currentPerf.Update(process);
                            }
                            iterationList.Add(currentPerf);
                        }

                        // Remove any processes that have been killed
                        foreach (Performance perfToRemove in _cpuList.FindAll(p => !iterationList.Contains(p)))
                        {
                            _cpuList.Remove(perfToRemove);
                        }


                        // Sort by CPU usage, then copy to memory list and sort by memory
                        _cpuList.Sort(delegate(Performance p1, Performance p2)
                        {
                            return p1.PercentProc.CompareTo(p2.PercentProc) * -1;
                        });
                        _memList.Clear();
                        _memList.AddRange(_cpuList);
                        _memList.Sort(delegate(Performance p1, Performance p2)
                        {
                            return p1.CurrentMemory.CompareTo(p2.CurrentMemory) * -1;
                        });
                    }
                }
            }
        }

        internal double Update()
        {
            return 0.0;
        }

        internal string GetString()
        {
            // Do a refresh (will only refresh if ReQuery = 1 and delay has been met)
            RefreshData();
            // If found a measure with ReQuery = 1
            if (HasData)
            {
                Measure dataMeasure = Plugin.Measures[DataProvider];

                // If delay has been met
                if (dataMeasure._updateIteration == dataMeasure.UpdateDivider)
                {
                    string returnString = string.Empty;
                    List<Performance> cpuList = dataMeasure._cpuList;
                    List<Performance> memList = dataMeasure._memList;

                    // Ensure in the bounds of the processes array
                    int origStartProc = StartProcNum;
                    int origEndProc = EndProcNum;

                    if (origStartProc >= cpuList.Count)
                        origStartProc = 0;

                    if (origEndProc >= cpuList.Count)
                        origEndProc = cpuList.Count - 1;

                    // Output process list
                    for (int i = origStartProc; i <= origEndProc; i++)
                    {
                        bool ignoredProcess = false;
                        if (SpecificIgnoredProcesses.Length > 0)
                        {
                            // Get name from appropriate array
                            string procName = string.Empty;
                            if (Type.Equals(MetricType.TopMemory))
                                procName = memList[i].Name;
                            else
                                procName = cpuList[i].Name;

                            // Determine if process is ignored
                            foreach (string s in SpecificIgnoredProcesses)
                            {
                                if (Utils.WildcardMatch(procName, s))
                                {
                                    // Adjust looping for ignored process
                                    ignoredProcess = true;
                                    origEndProc++;
                                    if (origEndProc >= cpuList.Count)
                                        origEndProc = cpuList.Count - 1;
                                    break;
                                }
                            }
                        }

                        // If not ignored, add it to the output
                        if (!ignoredProcess)
                        {
                            if (Type.Equals(MetricType.TopMemory))
                                returnString += memList[i].ToString(Format);
                            else
                                returnString += cpuList[i].ToString(Format);

                            returnString += Environment.NewLine;
                        }
                    }

                    // Trim off any newlines
                    lastData = returnString.TrimEnd();
                }
            }

            // Always return something. If this is a delay tick, return cached data
            return lastData;
        }
    }

    public static class Plugin
    {
        internal static Dictionary<uint, Measure> Measures = new Dictionary<uint, Measure>();

        [DllExport]
        public unsafe static void Initialize(void** data, void* rm)
        {
            uint id = (uint)((void*)*data);
            Measures.Add(id, new Measure());
        }

        [DllExport]
        public unsafe static void Finalize(void* data)
        {
            uint id = (uint)data;
            Measures.Remove(id);
        }

        [DllExport]
        public unsafe static void Reload(void* data, void* rm, double* maxValue)
        {
            uint id = (uint)data;
            Measures[id].Reload(new Rainmeter.API((IntPtr)rm), ref *maxValue);
        }

        [DllExport]
        public unsafe static double Update(void* data)
        {
            uint id = (uint)data;
            return Measures[id].Update();
        }

        [DllExport]
        public unsafe static char* GetString(void* data)
        {
            uint id = (uint)data;
            fixed (char* s = Measures[id].GetString()) return s;
        }
    }
}
