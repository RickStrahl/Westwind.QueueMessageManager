using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace Westwind.MessageQueueing.Hosting
{
    /// <summary>
    /// This class is the service bootstrapper that loads up
    /// a QueueController and gets it processing queue items.
    /// 
    /// Provive a Multi-Controller type and override the ManagerType
    /// or the OnCreateManager predicate to customize the controller.
    /// 
    /// This class is instantiated at the launch of the server -
    /// in Application_Start or in the oWin bootstrap process 
    /// when self hosting.
    /// </summary>
    public class ServiceLauncher<TController>
        where TController:  QueueControllerMultiple, new()        
    {

        public ILogger LogManager { get; }

        QueueMessageManagerConfiguration QueueManagerConfiguration { get;  }

        public ServiceLauncher()
        {
            LogManager = new NullLogger<ServiceLauncher<TController>>();
        }


        public ServiceLauncher(QueueMessageManagerConfiguration config)
        {
            QueueManagerConfiguration = config;
        }

        /// <summary>
        /// Instance of the QueueService controller that is maintained
        /// on this service instance - ensures the controller's lifetime
        /// is tied to the service.
        /// </summary>
        TController Controller { get; set; }

        /// <summary>
        /// QueueManager Type to instantiate (defaults to QueueManagerSql)
        /// 
        /// Use this or OnCreateQueueManager to instantiate the
        /// appropriate QueueManager type
        /// </summary>
        public Type QueueManagerType { get; set;  }

        /// <summary>
        /// Optional expression used to create a QueueManager Instance
        /// for each controller.
        /// </summary>
        public Func<QueueMessageManager> OnCreateQueueManager { get; set;  }

        public void Start()
        {                
            try
            {                
                // Create multiple child controllers from web.config configuration                
                Controller = new TController()
                {
                    QueueManagerType = QueueManagerType,
                    OnCreateQueueManager = OnCreateQueueManager
                };
                Controller.Initialize();                

                // *** Spin up n Number of threads to process requests
                Controller.StartProcessingAsync();

                // Create a log entry to show which Queues and what their settigs are.
                var sb = new StringBuilder();

                foreach (QueueController controller in Controller.Controllers)
                {
                   sb.AppendLine($" [ {controller.ThreadCount} thread(s) on Queue: {controller.QueueName} ] ");
                }
                LogManager.LogInformation($"QueueManager Controller Started:\n{sb.ToString()}");

                // Allow access to a global instance of this controler and service
                // So we can access it from the stateless SignalR hub
                QmmGlobals.Controller = Controller;                
            }
            catch (Exception ex)
            {
                LogManager.LogError(ex, ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Stops the Controllers and shuts down all monitoring/
        /// processing threads.
        /// </summary>
        /// <param name="immediate"></param>
        public void Stop(bool immediate = false)
        {
            LogManager.LogInformation("QueueManager Controller Stopped.");

            Controller.StopProcessing();
            Controller.Dispose();
            Thread.Sleep(1500);            
        }

    }
}