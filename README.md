TopProcesses.dll
================

A Rainmeter plugin to show n-number of top processes by either memory or CPU consumption.

The plugin was created by Chad Voelker and made compatible with Rainmeter 3.2 by Grant Pannell.

The only source I found were for version 2.0.0, not compatible with Rainmeter 3.2. I decompiled the 2.2.0 DLL 
in order to have a working base for the changes I needed for [Rainmeter-GalaxyS](https://github.com/mistic100/rainmeter-GalaxyS) 
and correct a bug in the `Format` parser.

# Usage

The example bellow display the top four processes by CPU usage.

```
12,8%: Photoshop
9,2%: firefox
3,4%: MsMpEng
0,9%: Spotify
```

```ini
[MeasureTopCPU]
Measure=Plugin
Plugin=Plugins\TopProcesses.dll

; Indicates if this init entry should re-look at the process list
; If you do more than one config entry, only ONE needs to do the ReQuery
; 1 = Yes, 0 = No
ReQuery=1

; Pipe-delimited processess to exclude from the list (can handle wildcards, use the % or * character)
; GlobalIgnoredProcesses is MORE efficient than SpecificIgnoredProcesses as it filters globally (at the Perfmon query)
; If you use GlobalIgnoredProcesses to filter, processes will be excluded from all measures using the TopProcesses plugin
; GlobalIgnoredProcesses will only be applied to the measure with ReQuery=1
GlobalIgnoredProcesses=Idle|%Total|rundll32|wscript|userinit|Rainmeter|svchost*

; Pipe-delimited processess to exclude from the list (can handle wildcards, use the % or * character)
; SpecificIgnoredProcesses is LESS efficient than GlobalIgnoredProcesses as it filters per measure.
; The processes below will ONLY be filtered for this measure
; SpecificIgnoredProcesses can be used on any measure, regardless of ReQuery value
SpecificIgnoredProcesses=

; Metric for which to determine top processes (CPU or Memory)
MetricType=CPU

; The top processes to find can be a single number (e.g. 0 = top one process) or a range (0-4 = top five processes)
ProcNums=0-3

; Format in which to return the results... any string including the following keys: %pName %pID %CPU %Memory
; You can also get a substring of a key; e.g. to trim the name to 8 chars use this format: s(%pName,0,7)
Format="%CPU%: %pName"

[TopCPUText]
Meter=String
MeterStyle=Style
MeasureName=MeasureTopCPU
```

## Advanced usage

If you want more control on the data display (eg: number of decimals, decimal symbol) you can use the `%RawCPU` and `%RawMemory` formats. When used with a single process (`ProcNums=0`) these will return a numeric value which can be formatted as needed.

The example bellow displays the top two processes by memory usage, with different color on the process names and Rainmeter's autoscale.

```ini
; This measure is not used directly, exist to fetch data
[MeasureTopMain]
Measure=Plugin
Plugin=Plugins\TopProcesses.dll
ReQuery=1
GlobalIgnoredProcesses=Idle|%Total|rundll32|wscript|userinit|dwm|Rainmeter|svchost*|System
UpdateDivider=30

; MEASURES
[MeasureTopMemValue1]
Measure=Plugin
Plugin=Plugins\TopProcesses.dll
MetricType=Memory
ProcNums=0
Format="%RawMemory"

[MeasureTopMemValue2]
Measure=Plugin
Plugin=Plugins\TopProcesses.dll
MetricType=Memory
ProcNums=1
Format="%RawMemory"

[MeasureTopMemName1]
Measure=Plugin
Plugin=Plugins\TopProcesses.dll
MetricType=Memory
ProcNums=0
Format="%pName"

[MeasureTopMemName2]
Measure=Plugin
Plugin=Plugins\TopProcesses.dll
MetricType=Memory
ProcNums=1
Format="%pName"

; METERS
[TopMemValue1]
Meter=String
MeasureName=MeasureTopMemValue1
AutoScale=1
StringAlign=Right
Text="%1B"
X=0
Y=0
H=30

[TopMemValue2]
Meter=String
MeasureName=MeasureTopMemValue2
AutoScale=1
StringAlign=Right
Text="%1B"
X=0r
Y=0R
H=30

[TopMemName1]
Meter=String
MeasureName=MeasureTopMemName1
FontColor=200,100,100
FontSize=14
ClipString=1
X=0r
Y=-30r
W=150
H=30

[TopMemName2]
Meter=String
MeasureName=MeasureTopMemName2
FontColor=200,100,100
FontSize=14
ClipString=1
X=0r
Y=0R
W=150
H=30
```

# License

Unknown