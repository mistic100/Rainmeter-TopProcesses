using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

        // Object used to query the database
        private DataThread DataThread;
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
            if (reQuery.Equals("1") || reQuery.ToLowerInvariant().Equals("true"))
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
                bool Dedupe = true;
                string DedupeParam = api.ReadString("Dedupe", string.Empty);
                if (DedupeParam.Equals("0") || DedupeParam.ToLowerInvariant().Equals("false"))
                {
                    Dedupe = false;
                }

                bool Async = false;
                string AsyncParam = api.ReadString("Async", string.Empty);
                if (AsyncParam.Equals("1") || AsyncParam.ToLowerInvariant().Equals("true"))
                {
                    Async = true;
                }

                string GlobalIgnoredProcesses = api.ReadString("GlobalIgnoredProcesses", string.Empty);

                this.DataThread = new DataThread(this.rm, Async, Dedupe, GlobalIgnoredProcesses);
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

        internal double Update()
        {
            // Do a refresh if ReQuery = 1
            if (this.ReQuery)
            {
                this.DataThread.Query();
            }

            // If found a measure with ReQuery = 1
            if (this.HasData)
            {
                Measure measure = Plugin.Measures[this.DataProvider];
                List<Performance.Data> cpuList = measure.DataThread.GetCpuList();
                List<Performance.Data> memList = measure.DataThread.GetMemList();

                // Return numeric data if only one raw data
                if (this.Format.Equals(Performance.FORMAT_CPU_RAW) && this.StartProcNum.Equals(this.EndProcNum))
                {
                    if (this.StartProcNum >= cpuList.Count)
                    {
                        return 0.0;
                    }
                    else
                    {
                        return cpuList[this.StartProcNum].PercentProc * 100;
                    }
                }
                if (this.Format.Equals(Performance.FORMAT_MEMORY_RAW) && this.StartProcNum.Equals(this.EndProcNum))
                {
                    if (this.StartProcNum >= memList.Count)
                    {
                        return 0.0;
                    }
                    else
                    {
                        return memList[this.StartProcNum].Memory;
                    }
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
