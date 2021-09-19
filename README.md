# HardwareSupervisor

*Version 0.1.0*

HardwareSupervisor is a windows service which will monitor Hardware state of the machine in which
is installed.

## Idea behind it ##
I was a lot frustrating to use an Administrator account to run all hardware monitoring
software like Open Hardware Monitor or others. For security reasons it's quite normal today to use a
normal account to achieve all day operations, so if you want to run hardware monitoring software you
have to enter administrator password to continue. But with a windows service this step is no more
necessary! So here it is HardwareSupervisor.

## Dependencies ##
Right now HardwareSupervisor makes use of OpenHardwareMonitor library: https://openhardwaremonitor.org so
all sensors supported by OpenHardwareMonitor are also available in HardwareSupervisor, and
all sensors that aren't supported by OpenHardwareMonitor aren't supported by HardwareSupervisor.

## Connection to other softwares ##
Information are published through Windows Management Instrumentation(WMI) protocol at
namespace `root/HardwareSupervisor` and are available to anyone without the need to be
an Administrator account.

### Power Shell ###
A simple way to query HardwareSupervisor is via Power Shell:
```console
 PS C:\temp> get-wmiobject -namespace "root/HardwareSupervisor" -Class Sensor
```
It's also a great way to see if HardwareSupervisor is working.

### HardwareSupervisorRainmeterPlugin ###
Another great way to see data collected by HardwareSupervisor is to use
HardwareSupervisorRainmeterPlugin: https://github.com/darkbrain-fc/HardwareSupervisorRainmeterPlugin

## Future goals ##
* The most interest future work will insist to implement am automatic FAN controller to be able to
control PC temperatures and finally remove all bloat software that usually does this job.
* More sensors support should also be a great value.
