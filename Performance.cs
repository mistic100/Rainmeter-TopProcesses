using System;
using System.Collections.Generic;

namespace PluginTopProcesses
{
    public class Performance
    {
        public const string FORMAT_NAME = "%pName";
        public const string FORMAT_ID = "%pID";
        public const string FORMAT_CPU_PERCENT = "%CPU";
        public const string FORMAT_CPU_RAW = "%RawCPU";
        public const string FORMAT_MEMORY = "%Memory";
        public const string FORMAT_MEMORY_RAW = "%RawMemory";

        public class Data
        {
            public int ProcessId;
            public string Name;
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

        public Performance(int procId, string name, Int64 timestamp, Int64 procTime, Int64 procMem)
        {
            this.Name = name;
            this.ProcessId = procId;
            this.Update(timestamp, procTime, procMem);
        }

        public void Update(Int64 timestamp, Int64 procTime, Int64 procMem)
        {
            this.PreviousProcTime = this.CurrentProcTime;
            this.PreviousTimeStamp = this.CurrentTimeStamp;
            this.CurrentTimeStamp = timestamp;
            this.CurrentProcTime = procTime;
            this.CurrentMemory = procMem;
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
