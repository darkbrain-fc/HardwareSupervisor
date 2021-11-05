# HardwareSupervisor

HardwareSupervisor is a windows service that monitor the Hardware state of your machine.

## Idea behind it ##
I was a lot frustrating to use an Administrator account to run all hardware monitoring
software like Open Hardware Monitor or others. For security reasons it's quite normal today to use a
normal account to achieve all day operations, so if you want to run hardware monitoring software you
have to enter administrator password to continue. But with a windows service this step is no more
necessary! So here it is HardwareSupervisor.

## AutoFanControl ##
Dynamic Fan Control can be enabled setting autoFanControl to true in the config file.
### :fire: Beware!!! :fire: ###
A software failure or simply a wrong configuration may :fire: burn :fire: your hardware! So use it
at your own risk!
Let's see how to configure this feature. Here's an example of `config.yaml` file:
```yaml
# CamelCase format
autoFanControl: true
# Debug, Info, Warn, Error
logLevel: Debug
sensors:
    # CPU
    /lpc/it8631e/0:
        - temperature: 30
          load: 50
        - temperature: 50
          load: 60
        - temperature: 70
          load: 100
default:
    - temperature: 20
      load: 30
    - temperature: 30
      load: 40
    - temperature: 40
      load: 60
    - temperature: 70
      load: 100
```
Initially it is suggested to use a Debug `logLevel` to understand better what's going on.
In `sensors` key you can specify a different curve for each detected sensor. You can see which
sensors are detected simply running HardwareSupervisor at least one time and looking to it's log file,
you should see something like that:
```
2021-10-31 12:10:13.3405|INFO|AutoControls|Found sensor: /lpc/it8631e/0
2021-10-31 12:10:13.3405|INFO|AutoControls|Found sensor: /lpc/it8631e/1
2021-10-31 12:10:13.3405|INFO|AutoControls|Found sensor: /nvidiagpu/0
```
On my machines three sensors are detected: `/lpc/it8631e/0`, `/lpc/it8631e/1` and `/nvidiagpu/0`. This sensors are special,
they are temperature sensors with an enabled PWM controller unit to handle FAN spin, HardwareSupervisor can handle only these sensors.
`default` it's the default curve if no one can match.
You can specify all curve points through it's coordinates: temperature (in celsius) and load (in %).
You can add as many points as you like.
Keep in mind that if `temperature < min(temperature)`, it will be fixed to `load(min(temperature))` and if
`temperature > max(temperature)`, it will be fixed to `load(max(temperature))`. Look at a curve example
![curve](https://github.com/darkbrain-fc/HardwareSupervisor/blob/master/assets/curve.jpg)
So it's not necessary to introduce these points:
```yaml
    - temperature: 0
      load: 20
    - temperature: 100
      load: 100
```
### :fire: NVIDIA RTX users :fire: ###
Even if the sensor of your beloved video card is detected it will not work!!! It's not well supported by
OpenHardwareMonitor. But don't worry I've a patch for you that I will release soon.

### Live config ###
You can tweak your config file without the need to restart the service! All modifications will be applied
immediately if they are parsed correctly.

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
* ~~The most interest future work will insist to implement an automatic FAN controller to be able to
control PC temperatures and finally remove all bloat software that usually does this job.~~
* Replace OpenHardwareMonitor with LibreHardwareMonitor or a new OpenHardwareMonitor fork.
