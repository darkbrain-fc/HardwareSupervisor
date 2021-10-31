/*
    Licensed to the Apache Software Foundation (ASF) under one
    or more contributor license agreements.  See the NOTICE file
    distributed with this work for additional information
    regarding copyright ownership.  The ASF licenses this file
    to you under the Apache License, Version 2.0 (the
    "License"); you may not use this file except in compliance
    with the License.  You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing,
    software distributed under the License is distributed on an
    "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
    KIND, either express or implied.  See the License for the
    specific language governing permissions and limitations
    under the License.  
*/
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NLog;
using OpenHardwareMonitor.Hardware;

public class AutoFanControl : IDisposable
{
    private List<ISensor> m_sensors;
    private Dictionary<IHardware, List<ISensor>> m_hardware_with_controls;
    private Dictionary<IHardware, List<ISensor>> m_hardware_with_fans;
    private Dictionary<IHardware, List<ISensor>> m_hardware_with_temperatures;
    private bool m_sensors_ready;
    private ConfigurationManager m_configuration_manager;

    public AutoFanControl(IComputer computer, ConfigurationManager configurationManager)
    {        
        m_sensors = new List<ISensor>();
        m_hardware_with_controls = new Dictionary<IHardware, List<ISensor>>();
        m_hardware_with_fans = new Dictionary<IHardware, List<ISensor>>();
        m_hardware_with_temperatures = new Dictionary<IHardware, List<ISensor>>();
        m_sensors_ready = false;

        foreach (IHardware hardware in computer.Hardware)
            ComputerHardwareAdded(hardware);

        computer.HardwareAdded += ComputerHardwareAdded;
        computer.HardwareRemoved += ComputerHardwareRemoved;
        m_configuration_manager = configurationManager;
    }

    private float GetFANSpeed(List<TemperaturePoint> curve, float temperature)
    {
        TemperaturePoint tmin = curve[0];
        TemperaturePoint tmax = curve[curve.Count - 1];
        if (temperature < tmin.Temperature)
            return tmin.Load;
        if (temperature >= tmax.Temperature)
            return tmax.Load;

        for (int i = 0; i < curve.Count - 1; i++) {
            TemperaturePoint a = curve[0];
            TemperaturePoint b = curve[i + 1];
            if (temperature >= a.Temperature && temperature < b.Temperature) {
                float dx = b.Temperature - a.Temperature;
                float dy = b.Load - a.Load;
                float m = dy / dx;
                float q = m * a.Temperature - a.Load;
                return m * temperature - q;
            }
        }
        return tmax.Load;
    }

    public void Update()
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        if (!m_sensors_ready) {
            m_sensors_ready = true;
            CleanupSensors();
        }
        if (!m_configuration_manager.Configuration.AutoFanControl)
            return;

        foreach (KeyValuePair<IHardware, List<ISensor>> entry in m_hardware_with_controls) {
            IHardware hardware = entry.Key;
            List<ISensor> temperatures = m_hardware_with_temperatures[hardware];
            List<ISensor> fans = m_hardware_with_fans[hardware];
            Configuration configuration = m_configuration_manager.Configuration;
            foreach (ISensor control in entry.Value) {
                ISensor temperature = temperatures[control.Index];
                ISensor fan = fans[control.Index];
                if (control.Control != null && temperature.Value.HasValue) {
                    float fanLoad = 100f;
                    List<TemperaturePoint> curve;
                    if (!configuration.Sensors.TryGetValue(control.Hardware.Identifier.ToString() + "/" + control.Index, out curve) &&
                        !configuration.Sensors.TryGetValue(control.Hardware.Identifier.ToString(), out curve)) {
                        logger.Warn("Applying default curve for: " + control.Hardware.Identifier.ToString());
                        curve = configuration.Default;
                    }
                    fanLoad = GetFANSpeed(curve, temperature.Value.Value);
                    control.Control.SetSoftware(fanLoad);
                    logger.Debug("[" + control.Hardware.Name + "] " + control.Name + " -> " + temperature.Value + "°C " + fan.Value + " rpm control at " + control.Value + "%");
                }
            }
        }
    }

    private void CleanupSensors()
    {
        List<IHardware> hwToRemove = new List<IHardware>();
        List<ISensor> sensorsToRemove = new List<ISensor>();
        Logger logger = LogManager.GetCurrentClassLogger();
        foreach (IHardware hardware in m_hardware_with_fans.Keys) {
            if (!m_hardware_with_controls.ContainsKey(hardware)) {
                hwToRemove.Add(hardware);
            }
        }
        foreach (IHardware hardware in m_hardware_with_temperatures.Keys) {
            if (!m_hardware_with_controls.ContainsKey(hardware)) {
                hwToRemove.Add(hardware);
            }
        }
        foreach (IHardware hardware in hwToRemove) {
            m_hardware_with_controls.Remove(hardware);
        }
        foreach (KeyValuePair<IHardware, List<ISensor>> entry in m_hardware_with_controls) {
            foreach (ISensor sensor in entry.Value) {
                List<ISensor> sensors;
                if (m_hardware_with_fans.TryGetValue(sensor.Hardware, out sensors)) {
                    if (sensor.Index >= sensors.Count)
                        sensorsToRemove.Add(sensor);
                } else {
                    sensorsToRemove.Add(sensor);
                }
                if (m_hardware_with_temperatures.TryGetValue(sensor.Hardware, out sensors)) {
                    if (sensor.Index >= sensors.Count)
                        sensorsToRemove.Add(sensor);
                } else {
                    sensorsToRemove.Add(sensor);
                }
            }
        }
        foreach (ISensor sensor in sensorsToRemove) {
            List<ISensor> sensors;
            if (m_hardware_with_controls.TryGetValue(sensor.Hardware, out sensors)) {
                sensors.Remove(sensor);
            }
        }
        foreach (KeyValuePair<IHardware, List<ISensor>> entry in m_hardware_with_controls) {
            IHardware hardware = entry.Key;
            foreach (ISensor control in entry.Value) {
                string name = control.Hardware.Identifier.ToString();
                if (!Regex.Match(name, @"\d$").Success) {
                    name += "/" + control.Index;
                }
                logger.Info("Found sensor: " + control.Hardware.Name + " => " + name);
            }
        }
    }
    #region Eventhandlers

    private void ComputerHardwareAdded(IHardware hardware)
    {
        if (!Exists(hardware.Identifier.ToString())) {
            foreach (ISensor sensor in hardware.Sensors)
                HardwareSensorAdded(sensor);

            hardware.SensorAdded += HardwareSensorAdded;
            hardware.SensorRemoved += HardwareSensorRemoved;
        }

        foreach (IHardware subHardware in hardware.SubHardware)
            ComputerHardwareAdded(subHardware);
    }

    private void HardwareSensorAdded(ISensor sensor)
    {
        m_sensors.Add(sensor);

        if (sensor.SensorType == SensorType.Control) {
            List<ISensor> sensors;
            if (m_hardware_with_controls.TryGetValue(sensor.Hardware, out sensors)) {
                sensors.Add(sensor);
            } else {
                sensors = new List<ISensor>();
                sensors.Add(sensor);
                m_hardware_with_controls.Add(sensor.Hardware, sensors);
            }
        } else if (sensor.SensorType == SensorType.Fan) {
            List<ISensor> sensors;
            if (m_hardware_with_fans.TryGetValue(sensor.Hardware, out sensors)) {
                sensors.Add(sensor);
            } else {
                sensors = new List<ISensor>();
                sensors.Add(sensor);
                m_hardware_with_fans.Add(sensor.Hardware, sensors);
            }
        } else if (sensor.SensorType == SensorType.Temperature) {
            List<ISensor> sensors;
            if (m_hardware_with_temperatures.TryGetValue(sensor.Hardware, out sensors)) {
                sensors.Add(sensor);
            } else {
                sensors = new List<ISensor>();
                sensors.Add(sensor);
                m_hardware_with_temperatures.Add(sensor.Hardware, sensors);
            }
        }
    }

    private void ComputerHardwareRemoved(IHardware hardware)
    {
        hardware.SensorAdded -= HardwareSensorAdded;
        hardware.SensorRemoved -= HardwareSensorRemoved;

        foreach (ISensor sensor in hardware.Sensors)
            HardwareSensorRemoved(sensor);

        foreach (IHardware subHardware in hardware.SubHardware)
            ComputerHardwareRemoved(subHardware);

        RevokeInstance(hardware.Identifier.ToString());
    }

    private void HardwareSensorRemoved(ISensor sensor)
    {
        RevokeInstance(sensor.Identifier.ToString());
    }

    #endregion

    #region Helpers

    private bool Exists(string identifier)
    {
        return m_sensors.Exists(h => h.Identifier.ToString() == identifier);
    }

    private void RevokeInstance(string identifier)
    {
        int instanceIndex = m_sensors.FindIndex(
            item => item.Identifier.ToString() == identifier
        );

        if (instanceIndex == -1)
            return;

        m_sensors.RemoveAt(instanceIndex);
    }

    #endregion

    public void Dispose()
    {
        m_sensors = null;
    }
}

