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

        public QueueControllerMultiple()
        {
            Controllers = new List<QueueController>();            
        }

        /// <summary>
        /// Pass in a list of controllers and their configuration to
        /// start all of the controllers processing simultaneously
        /// </summary>
        /// <param name="controllers">List of pre-configured controllers</param>
        public QueueControllerMultiple(IEnumerable<QueueController> controllers, string connectionString = null)
        {
            Controllers = new List<QueueController>();            

            if (controllers == null)
                return;

            if (connectionString == null)
                connectionString = QueueMessageManagerConfiguration.Current.ConnectionString;

            foreach (var controller in controllers)
            {
                if (string.IsNullOrEmpty(controller.ConnectionString))
                    controller.ConnectionString = connectionString;
                if (controller.ManagerType == null)
                    controller.ManagerType = ManagerType;

                Controllers.Add(controller);
            }
            Controllers.AddRange(controllers);            
        }

        /// <summary>
        /// Loads configuration settings from configuration file and loads up
        /// the Controllers list.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="managerType"></param>
        public void Initialize(QueueMessageManagerConfiguration configuration = null, Type managerType = null)
        {            
            base.Initialize(configuration, managerType);

            // ignore controller list if controllers have been 
            // explicitly set
            if (Controllers != null && Controllers.Count > 0)
                return;

            // load up the controllers
            Controllers = new List<QueueController>();

            if (configuration == null)
                configuration = QueueMessageManagerConfiguration.Current;
            if (managerType == null)
                managerType = typeof(QueueMessageManagerSql);

            if (configuration != null && configuration.Controllers != null)
            {
                foreach (var config in configuration.Controllers)
                {
                    var ctrl = Activator.CreateInstance(GetType()) as QueueController;

                    ctrl.ConnectionString = string.IsNullOrEmpty(config.ConnectionString)
                        ? ConnectionString ?? ""
                        : config.ConnectionString;

                    ctrl.QueueName = config.QueueName;
                    ctrl.ThreadCount = config.ControllerThreads;
                    ctrl.WaitInterval = config.WaitInterval;
                    ctrl.ManagerType = managerType;
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
        public event Action<QueueMessageManager> ExecuteStart;

        /// <summary>
        /// Event fired when the asynch operation has successfully completed
        /// </summary>
        public event Action<QueueMessageManager> ExecuteComplete;

        /// <summary>
        /// Event fired when the asynch operation has failed to complete (an exception
        /// was thrown during processing). Implement for logging or notifications.
        /// </summary>
        public event Action<QueueMessageManager, Exception> ExecuteFailed;
        
        /// <summary>
        /// Event fired when the read operation to retrieve the next message from
        /// the database has failed. Allows for error handling or logging.
        /// </summary>
        public event Action<QueueMessageManager, Exception> NextMessageFailed;


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
