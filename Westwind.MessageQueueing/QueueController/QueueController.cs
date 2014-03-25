using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Westwind.Utilities;
using Westwind.Utilities.Logging;

namespace Westwind.MessageQueueing
{

    /// <summary>
    /// This class is a Server Controller that can be run by 
    /// a Host process to handle processing of message requests
    /// in Windows Forms/Service applications. It provides
    /// a multi-threaded server process that fires events
    /// when messages arrive in the queue and are completed.
    /// 
    /// A client application can simply drop this component
    /// into the app and attach to the events provdided here.
    /// </summary>
    public class QueueController : IDisposable        
    {
        public QueueController(QueueMessageManagerConfiguration configuration = null, Type queueManagerType = null)
        {
            // Poll once a second
            WaitInterval = 1000;
            QueueName = string.Empty;
            
            if (configuration == null)
                configuration = QueueMessageManagerConfiguration.Current;

            ManagerType = queueManagerType ?? typeof(QueueMessageManagerSql);
                        
            ConnectionString = configuration.ConnectionString;
            ThreadCount = configuration.ControllerThreads;
            QueueName = configuration.QueueName ?? string.Empty;
            WaitInterval = configuration.WaitInterval;
        }

        
        /// <summary>
        /// Connection String for the database
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Optional list of controllers passed into to the constructor
        /// </summary>
        protected IEnumerable<QueueController> Controllers { get; set; }

        /// <summary>
        /// Determines whether the controller is processing messages
        /// </summary>
        protected virtual bool Active {get; set;}
      

        /// <summary>
        /// determines if the service is paused
        /// </summary>        
        public virtual bool Paused {get; set;}
        
        
        /// <summary>
        /// Determines how often the control checks for new messages
        /// Set in milliseconds.
        /// </summary>
        public virtual int WaitInterval {get; set; }
        
        /// <summary>
        /// Number of threads processing the queue
        /// </summary>
        public virtual int ThreadCount { get; set; }

        /// <summary>
        /// Counter that keeps track of how many messages have been processed 
        /// since the server started.
        /// </summary>
        public virtual int MessagesProcessed { get; set; }

        /// <summary>
        /// Sets the types of messages that this controller is looking for
        /// </summary>
        public string QueueName {get; set; }

        /// <summary>
        /// The specific type of the message manager class
        /// </summary>
        public Type ManagerType { get; set; }

        /// <summary>
        /// Synchronous Message Processing routine - will process one message after
        /// another
        /// </summary>        
        public virtual void StartProcessing()
        {                        
            Active = true;
            Paused = false;

            while (Active)
            {                
                if (Paused)
                {
                    Thread.Sleep(WaitInterval);
                    continue;
                }

                // Start by retrieving the next message if any
                // ALWAYS create a new instance so the events get thread safe object
                QueueMessageManager manager = null;
                manager = ReflectionUtils.CreateInstanceFromType(ManagerType,ConnectionString ?? string.Empty) as QueueMessageManager;
                                            
    
                if (manager.GetNextQueueMessage(QueueName) == null)
                {
                    if (!string.IsNullOrEmpty(manager.ErrorMessage))
                        OnNextMessageFailed(manager, new ApplicationException(manager.ErrorMessage));                        

                    // Nothing to do - wait for next poll interval
                    Thread.Sleep(WaitInterval);
                    continue;
                }
                
                // Fire events to execute the real operations
                ExecuteSteps(manager);
                                
                // let CPU breathe
                Thread.Sleep(1);  
            } 
        }

        /// <summary>
        /// Shuts down the Message Processing loop
        /// </summary>
        public virtual void StopProcessing()
        {
            if (!OnStopProcessing())
                return;

            // next loop through checks will exit
            Active = false;

            // allow threads some time to shut down
            Thread.Sleep(1000);
        }
        
        /// <summary>
        /// Starts queue processing asynchronously on the specified number of threads.
        /// This is a common scenario for Windows Forms interfaces so the UI
        /// stays active while the application monitors and processes the
        /// queue on a separate non-ui thread
        /// </summary>
        public virtual void StartProcessingAsync(int threads = -1)
        {
            if (!OnStartProcessing())
                return;

            if (threads < 0)
                threads = ThreadCount;
                        
            if (threads < 1)
                threads = 1;

            ThreadCount = threads;

            for (int x = 0; x < threads; x++)
            {
                Thread th = new Thread(StartProcessing);
                th.Start();
            }
        }


        /// <summary>
        /// This is the 'handler' code that actually does processing work 
        /// It merely calls into any events that are hooked up to the controller
        /// for these events:
        /// 
        /// ExecuteStart
        /// ExecuteComplete
        /// ExecuteFailed
        /// </summary>
        /// <param name="manager">Instance of QueueMessageManager and it's Item property</param>
        protected virtual void ExecuteSteps(QueueMessageManager manager)
        {
            try
            {
                // Hook up start processing
                OnExecuteStart(manager);

                // Hookup end processing
                OnExecuteComplete(manager);
            }
            catch(Exception ex)
            {
                OnExecuteFailed(manager,ex);
            }

            MessagesProcessed++;
        }


        /// <summary>
        /// Event called when an individual request starts processing
        /// Your user code can attach to this event and start processing
        /// with the message information.
        /// </summary>        
        public virtual event  Action<QueueMessageManager> ExecuteStart;


        /// <summary>
        /// Override this method to process your async  operation. Required for
        /// anything to happen when the message is processed. If the operation 
        /// succeeds (no exception), OnExecuteComplete will
        /// be called. This method should throw an exception if the operation fails,
        /// so that OnExecuteFailed will be fired. 
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Item property to get access to the current method
        /// </param>
        protected virtual void OnExecuteStart(QueueMessageManager manager)
        {
            if (ExecuteStart != null)
                ExecuteStart(manager);
        }

        /// <summary>
        /// Event fired when the asynch operation has successfully completed
        /// </summary>
        public virtual event Action<QueueMessageManager> ExecuteComplete;

        /// <summary>
        /// Override this method to do any post processing that needs to happen
        /// after each async operation has successfully completed. Optional - use
        /// for things like logging or reporting on status.
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Item property to get access to the current method
        /// </param>
        protected virtual void OnExecuteComplete(QueueMessageManager Message)
        {
            if (ExecuteComplete != null)
                ExecuteComplete(Message);
        }

        /// <summary>
        /// Event fired when the asynch operation has failed to complete (an exception
        /// was thrown during processing). Implement for logging or notifications.
        /// </summary>
        public event Action<QueueMessageManager, Exception> ExecuteFailed;

        /// <summary>
        /// Override this method to handle any errors that occured during processing
        /// of the async task. Optional - implement for logging or notifications.
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Item property to get access to the current method
        /// </param>
        /// <param name="ex">
        /// Exeception that caused the operation to fail
        /// </param>
        protected virtual void OnExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            if (ExecuteFailed != null)
                ExecuteFailed(manager, ex);
        }

        /// <summary>
        /// Event fired when the read operation to retrieve the next message from
        /// the database has failed. Allows for error handling or logging.
        /// </summary>
        public virtual event Action<QueueMessageManager, Exception> NextMessageFailed;

        /// <summary>
        /// Override this method to handle any errors that occured trying to receive 
        /// the next message from the SQL table.
        /// 
        /// Allows for error handling or logging in your own applications.
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Item property to get access to the current method
        /// </param>
        /// <param name="ex">
        /// Exeception that caused the operation to fail
        /// </param>
        protected virtual void OnNextMessageFailed(QueueMessageManager manager, Exception ex)
        {
            if (NextMessageFailed != null)
                NextMessageFailed(manager, ex);
        }


        /// <summary>
        /// Method that is called just before the controller stops
        /// processing requests. Use to send messages.
        /// If you return false from this method the queue is not stoped.
        /// </summary>
        /// <returns></returns>
        protected virtual bool OnStopProcessing()
        {
            return true;
        }

        /// <summary>
        /// Method that is called just before the the controller
        /// starts up processing for the queue. If you return
        /// false from this method the controller queue is not
        /// started.
        /// </summary>
        /// <returns></returns>
        protected virtual bool OnStartProcessing()
        {
            return true;
        }

        /// <summary>
        /// Pauses processing by keeping the thread alive
        /// and waiting until the pause is unset
        /// </summary>
        /// <param name="pause"></param>
        public virtual void PauseProcessing(bool pause = true)
        {
            Paused = pause;
        }


        public virtual void Dispose()
        {
            StopProcessing();
        }

    }


}
