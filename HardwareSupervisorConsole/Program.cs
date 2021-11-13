using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System;

namespace HardwareSupervisorConsole
{
    class Program
    {
        class HardwareSupervisorTest : HardwareSupervisor.Service {
            public void Start() {
                string[] args = new string[1];
                args[0] = "console";
                OnStart(args);
            }
        }
        static void Main(string[] args)
        {
            HardwareSupervisorTest service = new HardwareSupervisorTest();
            Console.WriteLine("HardwareSupervisor. Press <Esc> to exit... ");
            service.Start();           
            while (Console.ReadKey().Key != ConsoleKey.Escape) { }
            service.Stop();
        }
}
}
