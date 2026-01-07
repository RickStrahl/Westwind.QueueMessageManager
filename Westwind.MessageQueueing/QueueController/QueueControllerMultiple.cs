using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Westwind.Utilities;

namespace Westwind.MessageQueueing
{
    public class QueueControllerMultiple : QueueController
    {        
        /// <summary>
        /// Child Controllers that are actually launched
        /// </summary>
        public List<QueueController> Controllers;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="config">Queue Manager Configuration </param>
        /// <param name="connectionString">Optional Connection String to override config value</param>
        /// <param name="controllers">Optional already configured list of controllers for processing</param>
        /// <param name="managerType">Optional manager type</param>
        public QueueControllerMultiple(QueueMessageManagerConfiguration config = null, 
            string connectionString = null, IEnumerable<QueueController> controllers = null, 
            Type managerType = null)
        {                        
            Initialize(config, connectionString, managerType, controllers);
        }


        /// <summary>
        /// Loads configuration settings from configuration file and loads up
        /// the Controllers list.
        /// </summary>
        /// <param name="configuration">Optional but recommended Configuration instance</param>
        /// <param name="connectionString">A connection string to override the config connection string</param>
        /// <param name="managerType"></param>
        /// <param name="controllers"></param>
        public void Initialize(QueueMessageManagerConfiguration configuration = null, string connectionString = null,
                               Type managerType = null, IEnumerable<QueueController> controllers = null)
        {            
            base.Initialize(configuration, connectionString, managerType);

            if (controllers != null)
                Controllers = controllers.ToList();
                

            // ignore controller list if controllers have been 
            // explicitly set
            if (Controllers != null && Controllers.Count > 0)
                return;

            
            if (configuration == null)
                configuration = QueueMessageManagerConfiguration.Current;
            if (managerType == null)
                managerType = QueueManagerType ?? typeof(QueueMessageManagerSql);


            // load up the controllers and create a single default controller
            Controllers = new List<QueueController>();
            
            if (configuration != null && configuration.Controllers != null)
            {
                // pass configuration to all the child controllers
                foreach (var config in configuration.Controllers)
                {
                    var ctrl = Activator.CreateInstance(QueueManagerType) as QueueController;
                    ctrl.Initialize(configuration, ConnectionString, QueueManagerType);
                    ctrl.OnCreateQueueManager = OnCreateQueueManager;

                    Controllers.Add(ctrl);
                }
            }         
        }


        /// <summary>
        /// Counter that keeps track of how many messages have been processed 
        /// since the server started.
        /// </summary>
        public override int MessagesProcessed
        {
            get
            {
                if (Controllers == null)
                {
                    Interlocked.Increment(ref _MessageProcessed);
                    return _MessageProcessed;
                }

                var count = 0;
                foreach (var controller in Controllers)
                    count += controller.MessagesProcessed;
                return count;
            }
        }
        private int _MessageProcessed = 0;


        /// <summary>
        /// Event called when an individual request starts processing
        /// Your user code can attach to this event and start processing
        /// with the message information.
        /// </summary>        
        public virtual event Action<QueueMessageManager> ExecuteStart;

        /// <summary>
        /// Event fired when the asynch operation has successfully completed
        /// </summary>
        public virtual event Action<QueueMessageManager> ExecuteComplete;
         
        /// <summary>
        /// Event fired when the asynch operation has failed to complete (an exception
        /// was thrown during processing). Implement for logging or notifications.
        /// </summary>
        public virtual event Action<QueueMessageManager, Exception> ExecuteFailed;
        
        /// <summary>
        /// Event fired when the read operation to retrieve the next message from
        /// the database has failed. Allows for error handling or logging.
        /// </summary>
        public virtual event Action<QueueMessageManager, Exception> NextMessageFailed;


        /// <summary>
        /// Starts all of the controllers processing requests on 
        /// a sepearate thread
        /// </summary>
        public void StartProcessingAsync()
        {
            foreach (QueueController controller in Controllers)
            {
                if (ExecuteStart != null)
                    controller.ExecuteStart += ExecuteStart;
                if (ExecuteComplete != null)
                    controller.ExecuteComplete += ExecuteComplete;
                if (ExecuteFailed != null)
                    controller.ExecuteFailed += ExecuteFailed;
                if (NextMessageFailed != null)
                    controller.NextMessageFailed += NextMessageFailed;

                controller.StartProcessingAsync();
            }
        }

        /// <summary>
        /// Stops all queue requests from processing  and ends
        /// the thread holding the queue controllers.
        /// </summary>
        public void StopProcessing()
        {
            foreach (QueueController controller in Controllers)
            {
                controller.StopProcessing();
            }
        }

        public override void PauseProcessing(bool pause = true)
        {
            foreach (var controller in Controllers)
                controller.Paused = pause;
            
        }        

    }
}
