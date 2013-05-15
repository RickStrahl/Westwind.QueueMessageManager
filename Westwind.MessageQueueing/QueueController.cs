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
        public QueueController()
        {
            // Poll once a second
            WaitInterval = 1000;
            QueueName = string.Empty;
        }

        public QueueController(string connectionString) : this()
        {
            ConnectionString = connectionString;
        }
        
        /// <summary>
        /// Connection String for the database
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Determines whether the controller is processing messages
        /// </summary>
        public bool Active {get; set; }

        /// <summary>
        /// determines if the service is paused
        /// </summary>
        public bool Paused { get; set; }
        
        /// <summary>
        /// Determines how often the control checks for new messages
        /// Set in milliseconds.
        /// </summary>
        public int WaitInterval {get; set; }
        
        /// <summary>
        /// Number of threads processing the queue
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Counter that keeps track of how many messages have been processed 
        /// since the server started.
        /// </summary>
        public int MessagesProcessed {get; set; }

        /// <summary>
        /// Sets the types of messages that this controller is looking for
        /// </summary>
        public string QueueName {get; set; }


        /// <summary>
        /// Synchronous Message Processing routine - will process one message after
        /// another
        /// </summary>        
        public void StartProcessing()
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
                QueueMessageManager manager = new QueueMessageManager(ConnectionString);
                
                if (manager.GetNextQueueMessage(QueueName) == null)
                {
                    if (!string.IsNullOrEmpty(manager.ErrorMessage))
                        this.OnNextMessageFailed(manager, new ApplicationException(manager.ErrorMessage));                        

                    // Nothing to do - wait for next poll interval
                    Thread.Sleep(WaitInterval);
                    continue;
                }
                
                // Fire events to execute the real operations
                ExecuteSteps(manager);
                
                // Give up a tiny time slice to avoid CPU bloat
                Thread.Sleep(1);
            } 
        }

        /// <summary>
        /// Shuts down the Message Processing loop
        /// </summary>
        public void StopProcessing()
        {
            if (!OnStopProcessing())
                return;

            // next loop through checks will exit
            this.Active = false;            
        }
        
        /// <summary>
        /// Starts queue processing asynchronously on the specified number of threads.
        /// This is a common scenario for Windows Forms interfaces so the UI
        /// stays active while the application monitors and processes the
        /// queue on a separate non-ui thread
        /// </summary>
        public void StartProcessingAsync(int threads = -1)
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
                Thread th = new Thread(new ThreadStart(this.StartProcessing));
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
        /// <param name="manager">Instance of QueueMessageManager and it's Entity property</param>
        protected virtual void ExecuteSteps(QueueMessageManager manager)
        {
            try
            {
                // Hook up start processing
                this.OnExecuteStart(manager);

                // Hookup end processing
                this.OnExecuteComplete(manager);
            }
            catch(Exception ex)
            {
                this.OnExecuteFailed(manager,ex);
            }

            this.MessagesProcessed++;
        }


        /// <summary>
        /// Event called when an individual request starts processing
        /// Your user code can attach to this event and start processing
        /// with the message information.
        /// </summary>        
        public event  Action<QueueMessageManager> ExecuteStart;


        /// <summary>
        /// Override this method to process your async  operation. Required for
        /// anything to happen when the message is processed. If the operation 
        /// succeeds (no exception), OnExecuteComplete will
        /// be called. This method should throw an exception if the operation fails,
        /// so that OnExecuteFailed will be fired. 
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Entity property to get access to the current method
        /// </param>
        protected virtual void OnExecuteStart(QueueMessageManager manager)
        {
            if (this.ExecuteStart != null)
                this.ExecuteStart(manager);
        }

        /// <summary>
        /// Event fired when the asynch operation has successfully completed
        /// </summary>
        public event Action<QueueMessageManager> ExecuteComplete;

        /// <summary>
        /// Override this method to do any post processing that needs to happen
        /// after each async operation has successfully completed. Optional - use
        /// for things like logging or reporting on status.
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Entity property to get access to the current method
        /// </param>
        protected virtual void OnExecuteComplete(QueueMessageManager Message)
        {
            if (this.ExecuteComplete != null)
                this.ExecuteComplete(Message);
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
        /// QueueManager instance. Use its Entity property to get access to the current method
        /// </param>
        /// <param name="ex">
        /// Exeception that caused the operation to fail
        /// </param>
        protected virtual void OnExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            if (this.ExecuteFailed != null)
                this.ExecuteFailed(manager, ex);
        }

        /// <summary>
        /// Event fired when the read operation to retrieve the next message from
        /// the database has failed. Allows for error handling or logging.
        /// </summary>
        public event Action<QueueMessageManager, Exception> NextMessageFailed;

        /// <summary>
        /// Override this method to handle any errors that occured trying to receive 
        /// the next message from the SQL table.
        /// 
        /// Allows for error handling or logging in your own applications.
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Entity property to get access to the current method
        /// </param>
        /// <param name="ex">
        /// Exeception that caused the operation to fail
        /// </param>
        protected virtual void OnNextMessageFailed(QueueMessageManager manager, Exception ex)
        {
            if (this.NextMessageFailed != null)
                this.NextMessageFailed(manager, ex);
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


        public virtual void Dispose()
        {
            this.StopProcessing();
        }

    }


#if false
        
        /// <summary>
        /// The maximum number of threads for the pool. The thread size
        /// determines the maximum number of requests that can be processed
        /// side by side
        /// </summary>
        public int MaxThreadPoolSize
        {
            get { return _MaxThreadPoolSize; }
            set { _MaxThreadPoolSize = value; }
        }
        private int _MaxThreadPoolSize = 3;


        /// <summary>
        /// Internal property to track current number of threads
        /// for the delegate threadpool
        /// </summary>
        private int ThreadCount = 0;   
        /// <summary>
        /// Uses async delegates to execute QueueMessage operations. Keeps tracks
        /// of active requests operating and limits the number delegates that get created
        /// another
        /// </summary>
        /// <param name="ThreadCount">Number of simultaneous requests to process</param>
        public void StartProcessingPool()
        {            
            this.Active = true;
            while (this.Active)
            {
                if (this.ThreadCount > this.MaxThreadPoolSize)
                {
                    Thread.Sleep(10);
                    continue;
                }

                QueueMessageManager MessageServer = new QueueMessageManager(this.ConnectionString);
                if (!MessageServer.GetNextQueueMessage(this.MessageType))
                {
                    // Nothing to do - wait for next poll interval
                    Thread.Sleep(this.PollInterval);
                    continue;
                }

                this.ThreadCount++;

                // Run asyncronously
                delExecuteWithMessage delExecute = new delExecuteWithMessage(this.ExecuteSteps);
                delExecute.BeginInvoke(MessageServer, this.ExecuteStepsComplete, null);
            } 
        }

        /// <summary>
        /// Callback on completion of ExecuteSteps Delegate. Used
        /// to update the thread counter
        /// </summary>
        /// <param name="ar"></param>
        void ExecuteStepsComplete(IAsyncResult ar)
        {
            this.ThreadCount--;
        }

#endif
}
