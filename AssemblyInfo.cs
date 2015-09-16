using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyCopyright("© 2012 - Chad Voelker with help (version 0.032 & 0.033) from DigitalDJ from the Rainmeter Forums (http://rainmeter.net/forum/viewtopic.php?f=18&t=239)")]
// 0.01 = Initial Version
// 0.02 = UpdateDivider and [Global]IgnoredProcesses wildcard support
// 0.03 = Add substring functionality s(key,startChar,endChar)
// 0.031 = Bugfix for number of processors
// 0.032 = Fix for MEM and CPU metrics at same time, fix for multiple meters on one metric
// 0.033 = SpecificIgnoredProcesses works per for measures with ReQuery=0
// 1.00 = Rename IgnoredProcesses to GlobalIgnoredProcesses and fix. Hooked into Rainmeter's logging framwork
// 2.0.0.0 = Ported to new SDK, Setting parsing rewrite, fixed sorting %Total, Less CPU usage on each update, using Private Working Set for Memory (same as taskmgr)
[assembly: AssemblyVersion("2.0.0.0")]

// Do not change the entries below!
#if X64
[assembly: AssemblyInformationalVersion("3.0.2.2161 (64-bit)")]
#else
[assembly: AssemblyInformationalVersion("3.0.2.2161 (32-bit)")]
#endif
[assembly: AssemblyProduct("Rainmeter")]
[assembly: AssemblyTitleAttribute("TopProcesses")]
[assembly: AssemblyDescriptionAttribute("Show n-number of top processes by either memory or CPU consumption.")]
