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
    public class ServiceLauncher<TQueueContainer>
        where TQueueContainer:  QueueContainer, new()        
    {
        public ILogger LogManager { get; }

        QueueMessageManagerConfiguration QueueManagerConfiguration { get;  }

        public ServiceLauncher()
        {
            LogManager = new NullLogger<ServiceLauncher<TQueueContainer>>();
        }

        public ServiceLauncher(TQueueContainer queueContainer, 
                               QueueMessageManagerConfiguration config)
        {
            Container = queueContainer;
            QueueManagerConfiguration = config;
        }

        /// <summary>
        /// Instance of the QueueService controller that is maintained
        /// on this service instance - ensures the controller's lifetime
        /// is tied to the service.
        /// </summary>
        TQueueContainer Container { get; set; }

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
                if (Container == null)
                    Container = new TQueueContainer();            

                // *** Spin up n Number of threads to process requests
                Container.StartProcessingAsync();

                // Create a log entry to show which Queues and what their settigs are.
                var sb = new StringBuilder();

                foreach (QueueController controller in Container.Controllers)
                {
                   sb.AppendLine($" [ {controller.ThreadCount} thread(s) on Queue: {controller.QueueName} ] ");
                }
                LogManager.LogInformation($"QueueManager Controller Started:\n{sb.ToString()}");

                // Allow access to a global instance of this controler and service
                // So we can access it from the stateless SignalR hub
                QueueContainer.Current = Container;                
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
            
            Container.StopProcessing();
            Container.Dispose();

            Thread.Sleep(1500);            
        }
    }
}