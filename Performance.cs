using System;
using System.Management;

namespace PluginTopProcesses
{
    public class Performance
    {
        private const string PROC_NAME = "Name";
        private const string PROC_ID = "IDProcess";
        private const string PROC_TIME = "PercentProcessorTime";
        private const string PROC_MEMORY = "WorkingSetPrivate";

        //: Anything including %pName %pID %CPU %Memory
        private const string FORMAT_NAME = "%pName";
        private const string FORMAT_ID = "%pID";
        private const string FORMAT_CPU_PERCENT = "%CPU";
        private const string FORMAT_MEMORY = "%Memory";
        private const string FORMAT_BEGIN_SAMPLE = "%StartSample";
        private const string FORMAT_END_SAMPLE = "%EndSample";

        private string[] _formatBytes = new string[] { "B", "KB", "MB", "GB" };

        private const string TIME_STAMP = "TimeStamp_Sys100NS";
        public string Name;
        public int ProcessId;
        public Int64 PreviousProcTime;
        public Int64 CurrentProcTime;
        public Int64 PreviousTimeStamp;
        public Int64 CurrentTimeStamp;
        private Int64 _currentMemory;
        public Int64 CurrentMemory
        {
            get { return _currentMemory; }
            set { _currentMemory = value; }
        }

        private double _percentProc;
        public double PercentProc
        {
            get { return _percentProc; }
            set { _percentProc = value; }
        }

        public Performance(ManagementObject proc)
        {
            this.Update(proc);
        }

        public void Update(ManagementObject proc)
        {
            PreviousProcTime = CurrentProcTime;
            PreviousTimeStamp = CurrentTimeStamp;
            Name = Convert.ToString(proc.GetPropertyValue(PROC_NAME));
            CurrentProcTime = Convert.ToInt64(proc.GetPropertyValue(PROC_TIME));
            CurrentTimeStamp = Convert.ToInt64(proc.GetPropertyValue(TIME_STAMP));
            CurrentMemory = Convert.ToInt64(proc.GetPropertyValue(PROC_MEMORY));
            ProcessId = Convert.ToInt32(proc.GetPropertyValue(PROC_ID));
            CalculateProcPercent();
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
                return this.Name.Equals(you.GetPropertyValue(PROC_NAME)) && this.ProcessId.Equals(Convert.ToInt32(you.GetPropertyValue(PROC_ID)));
            }
            return base.Equals(obj);
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}): {2}% {3}", Name, ProcessId, PercentProc.ToString("0.0"), ToByteString(CurrentMemory));
        }

        public string ToString(string format)
        {
            format = ReplaceString(format, FORMAT_CPU_PERCENT, PercentProc.ToString("0.0"));
            format = ReplaceString(format, FORMAT_ID, ProcessId.ToString());
            format = ReplaceString(format, FORMAT_MEMORY, ToByteString(CurrentMemory));
            format = ReplaceString(format, FORMAT_NAME, Name);
            format = ReplaceString(format, FORMAT_BEGIN_SAMPLE, PreviousTimeStamp.ToString());
            format = ReplaceString(format, FORMAT_END_SAMPLE, CurrentTimeStamp.ToString());
            return format;
        }

        private string ReplaceString(string format, string key, string measure)
        {
            int startIndex = format.IndexOf(key);
            if (format.IndexOf("s(", StringComparison.CurrentCultureIgnoreCase) == startIndex - 2)
            {
                startIndex = startIndex - 2;
                int endIndex = format.IndexOf(")", startIndex);
                string fullKey = format.Substring(startIndex, endIndex - startIndex + 1);
                string[] fullKeySplit = fullKey.Replace(" ", "").Split(new char[] { ',', ')', '(' });

                int subStrStart;
                int subStrEnd;
                if (!Int32.TryParse(fullKeySplit[2], out subStrStart) || !Int32.TryParse(fullKeySplit[3], out subStrEnd))
                {
                    subStrStart = 0;
                    subStrEnd = measure.Length;
                }
                else
                {
                    if (subStrStart < 0) subStrStart = 0;
                    if (subStrStart >= measure.Length) subStrStart = measure.Length - 1;
                    if (subStrEnd < 0) subStrEnd = 0;
                    if (subStrEnd >= measure.Length) subStrEnd = measure.Length - 1;
                }
                format = format.Replace(format.Substring(startIndex, endIndex - startIndex + 1), measure.Substring(subStrStart, subStrEnd - subStrStart + 1));
            }
            else
            {
                format = format.Replace(key, measure);
            }
            return format;
        }

        private string ToByteString(Int64 byteNum)
        {
            for (int i = _formatBytes.Length; i > 0; i--)
            {
                if (byteNum > Math.Pow(1024.0, i))
                {
                    return String.Format("{0:0.00 " + _formatBytes[i] + "}", (double)byteNum / Math.Pow(1024, i));
                }
            }

            return byteNum.ToString() + " " + _formatBytes[0];

        }

        private void CalculateProcPercent()
        {
            if (PreviousProcTime > 0 && PreviousTimeStamp > 0)
            {
                PercentProc = (double)(CurrentProcTime - PreviousProcTime) / (CurrentTimeStamp - PreviousTimeStamp) * 100 / Environment.ProcessorCount;
            }
            else
            {
                PercentProc = 0;
            }
        }
    }
}
