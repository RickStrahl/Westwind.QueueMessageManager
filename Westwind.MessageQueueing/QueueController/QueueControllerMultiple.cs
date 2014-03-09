using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Westwind.Utilities;

namespace Westwind.MessageQueueing
{
    public class QueueControllerMultiple : QueueController
    {
        public List<QueueController> Controllers;

        public QueueControllerMultiple(QueueMessageManagerConfiguration configuration = null, Type managerType = null) 
            : base(configuration, managerType)
        {
            Controllers = new List<QueueController>();
        }

        /// <summary>
        /// Pass in a list of controllers and their configuration to
        /// start all of the controllers processing simultaneously
        /// </summary>
        /// <param name="controllers">List of pre-configured controllers</param>
        public QueueControllerMultiple(IEnumerable<QueueController> controllers)
        {
            var controllerList = new List<QueueController>();
            foreach (var controller in controllers)
                controllerList.Add(controller);

            Controllers = controllerList;            
        }

  

      

        /// <summary>
        /// Counter that keeps track of how many messages have been processed 
        /// since the server started.
        /// </summary>
        public override int MessagesProcessed
        {
            get
            {
                var count = 0;
                foreach (var controller in Controllers)
                    count += controller.MessagesProcessed;
                return count;
            }
        }


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
