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
        private Computer m_computer;
        private WmiProvider m_wmiProvider;
        private Thread m_engineThread;
        private UpdateVisitor m_updateVisitor;
        private Logger m_logger;
        private bool m_exit;

        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
                m_exit = false;
                LoggingConfiguration config = new NLog.Config.LoggingConfiguration();
                FileTarget logfile = new NLog.Targets.FileTarget("logfile") { FileName = "HardwareSupervisor.log" };
                logfile.ArchiveEvery = FileArchivePeriod.Day;
                logfile.MaxArchiveDays = 7;
                config.AddRuleForAllLevels(logfile);
                NLog.LogManager.Configuration = config;
                m_logger = NLog.LogManager.GetCurrentClassLogger();
                m_logger.Info("Starting service");

                m_updateVisitor = new UpdateVisitor();
                m_computer = new Computer();
                m_computer.MainboardEnabled = true;
                m_computer.CPUEnabled = true;
                m_computer.RAMEnabled = true;
                m_computer.GPUEnabled = true;
                m_computer.FanControllerEnabled = true;
                m_computer.HDDEnabled = true;

                m_wmiProvider = new WmiProvider(m_computer);
                m_computer.HardwareAdded += new HardwareEventHandler(HardwareAdded);
                m_computer.HardwareRemoved += new HardwareEventHandler(HardwareRemoved);
                m_computer.Open();

                Instrumentation.Publish(m_wmiProvider);
                m_logger.Info("Service started");

                m_engineThread = new Thread(new ThreadStart(ThreadMain));
                m_engineThread.Start();
            }
            catch (Exception e)
            {
                m_logger.Error(e.Message);
                throw;
            }
        }

        private void HardwareAdded(IHardware hardware)
        {
            m_logger.Info("HardwareAdded: " + hardware.Name);
        }

        private void HardwareRemoved(IHardware hardware)
        {
            m_logger.Info("HardwareRemoved: " + hardware.Name);
        }

        protected override void OnStop()
        {
            try
            {
                m_logger.Info("Stopping service");

                m_exit = true;
                m_engineThread.Join();
                Instrumentation.Revoke(m_wmiProvider);

                m_logger.Info("Service stopped");
            }
            catch (Exception e)
            {
                m_logger.Error(e.Message);
            }
        }

        public void ThreadMain()
        {
            while (!m_exit)
            {
                m_computer.Accept(m_updateVisitor);
                if (m_wmiProvider != null)
                    m_wmiProvider.Update();
                Thread.Sleep(1000);
            }
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            m_logger.Info("Service recall");
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
