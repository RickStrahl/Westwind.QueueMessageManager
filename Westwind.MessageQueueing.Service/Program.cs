using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;
using Westwind.Utilities.Logging;
using Westwind.Windows.Services;

namespace Westwind.MessageQueueing.Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">
        /// Three positional parameters can be passed:
        ///  1  -  Message Type 
        ///  2  -  Thread Count  (2)
        ///  3  -  Poll Interval  (1000)
        /// </param>
        [STAThread]
        static void Main(string[] args)
        {
            string arg0 = string.Empty;
            if (args.Length > 0)
                arg0 = (args[0] ?? string.Empty).ToLower();

            if (arg0 == "-service")
            {
                RunService();
                return;
            }
            if (arg0 == "-fakeservice")
            {
                FakeRunService();
                return;
            }
            else if (arg0 == "-installservice" ||  arg0 == "-i")
            {
                WindowsServiceManager SM = new WindowsServiceManager();
                if (!SM.InstallService(Environment.CurrentDirectory + "\\MarvelPressQueueService.exe -service",
                        "MarvelPressQueueService", "Marvelpress Workflow Queue Manager Service"))
                    MessageBox.Show("Service install failed.");

                return;
            }
            else if (arg0 == "-uninstallservice" || arg0 == "-u")
            {
                WindowsServiceManager SM = new WindowsServiceManager();
                if (!SM.UnInstallService("MarvelPressQueueService"))
                    MessageBox.Show("Service failed to uninstall.");

                return;
            }

            RunDesktop(args);
        }

        static void RunDesktop(string[] args)
        {
            // Run Windows Form
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var queueManagerForm = new SampleQueueMessageManagerForm();

            int parmCount = args.Length;
            if (parmCount > 0)
                queueManagerForm.MessageType = args[0];
            if (parmCount > 1)
            {
                int threadCount = 1;
                if (int.TryParse(args[1], out threadCount))
                    queueManagerForm.ThreadCount = threadCount;
            }

            Application.Run(queueManagerForm);
        }


        static void RunService()
        {                                        
			var ServicesToRun = new ServiceBase[] { new QueueService() };
            LogManager.Current.LogInfo("Service Started.");            
			ServiceBase.Run(ServicesToRun);
        }

        static void FakeRunService()
        {
            var service = new QueueService();
            service.Start();            

            // never ends but waits
           Console.ReadLine();
        }

    }
}
