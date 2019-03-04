using System;
using System.Management;
using System.Collections.Generic;

namespace PluginTopProcesses
{
    public class Performance
    {
        public const string PROC_NAME = "Name";
        private const string PROC_ID_OLD = "IDProcess";
        private const string PROC_ID_NEW = "ProcessID";
        private const string PROC_TIME = "PercentProcessorTime";
        private const string PROC_MEMORY_OLD = "WorkingSet";
        private const string PROC_MEMORY_NEW = "WorkingSetPrivate";
        private const string TIME_STAMP = "TimeStamp_Sys100NS";

        public const string FORMAT_NAME = "%pName";
        public const string FORMAT_ID = "%pID";
        public const string FORMAT_CPU_PERCENT = "%CPU";
        public const string FORMAT_CPU_RAW = "%RawCPU";
        public const string FORMAT_MEMORY = "%Memory";
        public const string FORMAT_MEMORY_RAW = "%RawMemory";

        public class Data
        {
            public string Name;
            public int ProcessId;
            public Int64 Memory;
            public double PercentProc;

            public void Add(Data data)
            {
                this.Memory+= data.Memory;
                this.PercentProc += data.PercentProc;
            }

            public override string ToString()
            {
                return this.ToString(FORMAT_NAME + " (" + FORMAT_ID + "): " + FORMAT_CPU_PERCENT + "% " + FORMAT_MEMORY);
            }

            public string ToString(string format)
            {
                format = Utils.ReplaceString(format, FORMAT_NAME, this.Name);
                format = Utils.ReplaceString(format, FORMAT_ID, this.ProcessId.ToString());
                format = Utils.ReplaceString(format, FORMAT_CPU_PERCENT, this.PercentProc.ToString("0.0"));
                format = Utils.ReplaceString(format, FORMAT_CPU_RAW, (this.PercentProc * 100).ToString());
                format = Utils.ReplaceString(format, FORMAT_MEMORY, Utils.ToByteString(this.Memory));
                format = Utils.ReplaceString(format, FORMAT_MEMORY_RAW, this.Memory.ToString());
                return format;
            }
        }

        public string Name;
        public int ProcessId;
        public Int64 PreviousProcTime;
        public Int64 CurrentProcTime;
        public Int64 PreviousTimeStamp;
        public Int64 CurrentTimeStamp;
        public Int64 CurrentMemory;
        public double PercentProc;

        private static string PropWorkingSet()
        {
            if (Environment.OSVersion.Version.Major > 5)
            {
                return PROC_MEMORY_NEW;
            }
            else
            {
                return PROC_MEMORY_OLD;
            }
        }

        private static string PropProcessId()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                return PROC_ID_NEW;
            }
            else
            {
                return PROC_ID_OLD;
            }
        }

        public Performance(ManagementObject proc)
        {
            this.Update(proc);
        }

        public void Update(ManagementObject proc)
        {
            this.PreviousProcTime = this.CurrentProcTime;
            this.PreviousTimeStamp = this.CurrentTimeStamp;
            this.Name = Convert.ToString(proc.GetPropertyValue(PROC_NAME));
            this.CurrentProcTime = Convert.ToInt64(proc.GetPropertyValue(PROC_TIME));
            this.CurrentTimeStamp = Convert.ToInt64(proc.GetPropertyValue(TIME_STAMP));
            this.CurrentMemory = Convert.ToInt64(proc.GetPropertyValue(PropWorkingSet()));
            this.ProcessId = Convert.ToInt32(proc.GetPropertyValue(PropProcessId()));
            this.CalculateProcPercent();
        }

        public Data ToData()
        {
            Data data = new Data();
            data.Name = this.Name;
            data.ProcessId = this.ProcessId;
            data.PercentProc = this.PercentProc;
            data.Memory = this.CurrentMemory;
            return data;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj.GetType(), typeof(Performance)))
            {
                Performance you = (Performance)obj;
                return this.Name.Equals(you.Name) && this.ProcessId.Equals(you.ProcessId);
            }
            else if (object.ReferenceEquals(obj.GetType(), typeof(ManagementObject)))
            {
                ManagementObject you = (ManagementObject)obj;
                return this.Name.Equals(you.GetPropertyValue(PROC_NAME)) && this.ProcessId.Equals(Convert.ToInt32(you.GetPropertyValue(PropProcessId())));
            }
            else
            {
                return base.Equals(obj);
            }
        }

        public override string ToString()
        {
            return this.ToData().ToString();
        }

        private void CalculateProcPercent()
        {
            if (this.PreviousProcTime > 0 && this.PreviousTimeStamp > 0)
            {
                this.PercentProc = (double)(this.CurrentProcTime - this.PreviousProcTime) / (this.CurrentTimeStamp - this.PreviousTimeStamp) * 100 / Environment.ProcessorCount;
            }
            else
            {
                this.PercentProc = 0;
            }
        }
    }
}
