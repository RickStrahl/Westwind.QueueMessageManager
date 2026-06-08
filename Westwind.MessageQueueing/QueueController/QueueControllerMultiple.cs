using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
            // top level configuration
            base.Initialize(configuration, connectionString, managerType);

            if (controllers != null)
                Controllers = controllers.ToList();


            // if controllers were passed in then we assume they are already configured and just use them
            if (Controllers != null && Controllers.Count > 0)
            {
                // fix up passed controllers for missing props
                foreach(var ctrl in Controllers) 
                {
                    if (string.IsNullOrEmpty(ctrl.ConnectionString))
                        ctrl.ConnectionString = connectionString;
                    if (ctrl.WaitInterval < 1)
                        ctrl.WaitInterval = WaitInterval;                    
                }

                return;
            }

            
            if (configuration == null)
                configuration = QueueMessageManagerConfiguration.Current;
            if (managerType == null)
                managerType = QueueManagerType ?? typeof(QueueMessageManagerSql);


            // load up the controllers and create a single default controller
            Controllers = new List<QueueController>();
            
            if (configuration?.Controllers != null)
            {
                // pass configuration to all the child controllers
                foreach (var config in configuration.Controllers)
                {
                    var ctrl = Activator.CreateInstance(typeof(QueueController)) as QueueController;
                    ctrl.InitializeIndiviualController( config, connectionString, managerType);
                    ctrl.ExecuteStart = OnExecuteStart;
                    ctrl.ExecuteStartAsync = OnExecuteStartAsync;
                    ctrl.ExecuteComplete = OnExecuteComplete;                        
                    ctrl.ExecuteFailed = OnExecuteFailed;

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


        ///// <summary>
        ///// Event called when an individual request starts processing
        ///// Your user code can attach to this event and start processing
        ///// with the message information.
        ///// </summary>        
        //public virtual event Action<QueueMessageManager> ExecuteStart;

        ///// <summary>
        ///// Event fired when the asynch operation has successfully completed
        ///// </summary>
        //public virtual event Action<QueueMessageManager> ExecuteComplete;
         
        ///// <summary>
        ///// Event fired when the asynch operation has failed to complete (an exception
        ///// was thrown during processing). Implement for logging or notifications.
        ///// </summary>
        //public virtual event Action<QueueMessageManager, Exception> ExecuteFailed;
        
        ///// <summary>
        ///// Event fired when the read operation to retrieve the next message from
        ///// the database has failed. Allows for error handling or logging.
        ///// </summary>
        //public virtual event Action<QueueMessageManager, Exception> NextMessageFailed;


        /// <summary>
        /// Starts all of the controllers processing requests on 
        /// a sepearate thread
        /// </summary>
            public void StartProcessingAsync()
            {
                foreach (QueueController controller in Controllers)
                {                    
                    controller.ExecuteStart = ExecuteStart;
                    controller.ExecuteStartAsync = ExecuteStartAsync;

                    controller.ExecuteComplete = ExecuteComplete;
                    controller.ExecuteFailed = ExecuteFailed;
                    controller.NextMessageFailed = NextMessageFailed;

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
