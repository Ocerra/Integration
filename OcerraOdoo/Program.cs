using Nancy.Hosting.Self;
using OcerraOdoo.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            var agentMode = args != null && args.Length > 0 ? args[0] : null;

            if (agentMode == "debug")
            {

                using (var host = new NancyHost(new Uri(Settings.Default.ManagementConsole)))
                {
                    try
                    {
                        Console.WriteLine("Strating web interface... Execute this line in CMD when you start service for the first time:");
                        Console.WriteLine("netsh http add urlacl url=\"http://+:1234/\" user=\"Everyone\"");
                        
                        host.Start();

                        Console.WriteLine("Running on " + Settings.Default.ManagementConsole);
                    }
                    catch (Exception ex) {
                        Helpers.LogError(ex, "Unable to start web interface");
                    }

                    Console.WriteLine("Press any keys to continue...");
                    var line = Console.ReadLine();
                    Console.WriteLine("Line: " + line);
                    Console.ReadLine();

                }
            }
            else {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new OcerraOdooService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
