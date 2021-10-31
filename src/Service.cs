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
using System.Globalization;
using System.Management.Instrumentation;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
using NLog;
using NLog.Config;
using NLog.Targets;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.WMI;

namespace HardwareSupervisor
{
    public partial class Service : ServiceBase
    {
        public const string LOG_FILE = "HardwareSupervisor.log";
        private Computer m_computer;
        private WmiProvider m_wmiProvider;
        private Thread m_engineThread;
        private UpdateVisitor m_updateVisitor;
        private bool m_exit;
        private ConfigurationManager m_configurationManager;
        private AutoFanControl m_autoControls;

        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Logger logger = null;
            try {
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
                m_exit = false;
                LoggingConfiguration config = new LoggingConfiguration();
                FileTarget logfile = new FileTarget("logfile") { FileName = LOG_FILE };
                logfile.ArchiveEvery = FileArchivePeriod.Day;
                logfile.MaxArchiveDays = 7;
                config.AddRuleForAllLevels(logfile);
                LogManager.Configuration = config;

                m_configurationManager = new ConfigurationManager();
                m_configurationManager.Changed += OnConfigurationChanged;
                m_configurationManager.Init();

                logger = LogManager.GetCurrentClassLogger();
                logger.Info("Starting service");
                m_updateVisitor = new UpdateVisitor();
                m_computer = new Computer();
                m_computer.MainboardEnabled = true;
                m_computer.CPUEnabled = true;
                m_computer.RAMEnabled = true;
                m_computer.GPUEnabled = true;
                m_computer.FanControllerEnabled = true;
                m_computer.HDDEnabled = true;
                m_autoControls = new AutoFanControl(m_computer, m_configurationManager);
                m_wmiProvider = new WmiProvider(m_computer);
                m_computer.HardwareAdded += new HardwareEventHandler(HardwareAdded);
                m_computer.HardwareRemoved += new HardwareEventHandler(HardwareRemoved);
                m_computer.Open();

                Instrumentation.Publish(m_wmiProvider);
                logger.Info("Service started");

                m_engineThread = new Thread(new ThreadStart(ThreadMain));
                m_engineThread.Start();
            } catch (Exception e) {
                if (logger != null) {
                    logger.Error(e.Message);
                }
                throw;
            }
        }

        private void OnConfigurationChanged(object sender, Configuration configuration)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            try {
                for (int i = 0; i < LogManager.Configuration.LoggingRules.Count; i++) {
                    LogManager.Configuration.LoggingRules[i].SetLoggingLevels(NLog.LogLevel.FromString(configuration.LogLevel), NLog.LogLevel.Fatal);
                }
                LogManager.ReconfigExistingLoggers();
                logger = LogManager.GetCurrentClassLogger();
            } catch (ArgumentException e) {
                logger.Error(e.Message);
            }
        }

        private void HardwareAdded(IHardware hardware)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("HardwareAdded: " + hardware.Name);
        }

        private void HardwareRemoved(IHardware hardware)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("HardwareRemoved: " + hardware.Name);
        }

        protected override void OnStop()
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            try {
                logger.Info("Stopping service");

                m_exit = true;
                m_engineThread.Join();
                Instrumentation.Revoke(m_wmiProvider);

                logger.Info("Service stopped");
            } catch (Exception e) {
                logger.Error(e.Message);
            }
        }

        public void ThreadMain()
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            try {
                while (!m_exit) {
                    m_computer.Accept(m_updateVisitor);
                    m_wmiProvider.Update();
                    m_autoControls.Update();
                    Thread.Sleep(1000);
                }
            } catch (Exception e) {
                logger.Error(e.Message);
                throw;
            }
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("Service recall");
        }
    }


    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
                subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }
}
