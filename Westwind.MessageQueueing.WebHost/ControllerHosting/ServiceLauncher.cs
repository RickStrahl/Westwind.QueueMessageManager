using System;
using System.Text;
using System.Threading;
using System.Web.Hosting;
using QueueManagerStarter;
using Westwind.Utilities.Logging;

namespace Westwind.MessageQueueing.WebHost.ControllerHosting
{
    /// <summary>
    /// Marvelpress Windows Service implementation that can be called 
    /// by a Windows service that has been launched.
    /// </summary>
    public class ServiceLauncher : IRegisteredObject
    {
        /// <summary>
        /// Instance of the QueueService controller that is maintained
        /// on this service instance - ensures the controller's lifetime
        /// is tied to the service.
        /// </summary>
        QueueControllerMultiple Controller { get; set; }       

        public void Start()
        {    
            try
            {
                //Controller = new MarvelPressWorkflowQueueController(controllers.ToArray());

                // Create multiple child controllers from web.config configuration
                Controller = new QueueMonitorMultipleQueueController();
                Controller.ManagerType = typeof(QueueMessageManagerSqlMsMq);
                
                Controller.Initialize();

                
                // *** Spin up n Number of threads to process requests
                Controller.StartProcessingAsync();

                // Create a log entry to show which Queues and what their settigs are.
                var sb = new StringBuilder();

                foreach (QueueController controller in Controller.Controllers)
                {
                   sb.AppendLine(String.Format(" [ {0} thread(s) on Queue: {1} ] ", controller.ThreadCount, controller.QueueName ));
                }
                LogManager.Current.LogInfo("QueueManager Controller Started", sb.ToString());

                // Allow access to a global instance of this controler and service
                // So we can access it from the stateless SignalR hub
                Globals.Controller = Controller;
                
            }
            catch (Exception ex)
            {
                LogManager.Current.LogError(ex);
                LogManager.Current.LogError(ex.GetBaseException());
            }
        }

    


        public void Stop(bool immediate = false)
        {
            LogManager.Current.LogInfo("QueueManager Controller Stopped.");

            Controller.StopProcessing();
            Controller.Dispose();
            Thread.Sleep(1500);

            HostingEnvironment.UnregisterObject(this); 
        }

    }
}