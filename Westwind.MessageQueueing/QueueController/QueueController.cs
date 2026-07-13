using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Westwind.Utilities;


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
        public const int DEFAULT_INTERVAL = 300;

        public QueueController()
        {            
            QueueName = string.Empty;
            WaitInterval = -1;
            ThreadCount = 1; 
            QueueManagerType = typeof(QueueMessageManagerSql);                        
            LogManager = new NullLogger<QueueController>();
        }


        public QueueController(ILogger logger)
        {
            LogManager = logger;
        }

        ///// <summary>
        ///// Initializes the top level QueueController from the 
        ///// Queue Configuration Settings
        ///// </summary>
        ///// <param name="configuration"></param>
        ///// <param name="queueManagerType"></param>
        //public void Initialize(QueueMessageManagerConfiguration configuration = null,  string connectionString = null, Type queueManagerType = null)
        //{                        
        //    if (queueManagerType != null)
        //        QueueManagerType = queueManagerType;
        //    if (QueueManagerType == null)
        //        QueueManagerType = typeof(QueueMessageManagerSql);

        //    if (configuration == null)
        //        configuration = QueueMessageManagerConfiguration.Current;

        //    if (configuration == null)
        //        return;
                    
        //    ConnectionString = connectionString ?? configuration.ConnectionString;
        //    ThreadCount = configuration.DefaultThreadCount;
        //    QueueName = configuration.DefaultControllerQueueName ?? string.Empty;
        //    WaitInterval = configuration.WaitInterval;            
        //}


        /// <summary>
        /// Configures a single controller with the configuration settings. This is called for each controller in the Controllers list
        /// </summary>       
        /// <param name="configuration"></param>
        /// <param name="connectionString"></param>
        /// <param name="queueManagerType"></param>
        public void InitializeIndiviualController(ControllerConfiguration configuration = null, string connectionString = null, Type queueManagerType = null)
        {            
            configuration = configuration ?? new();

            WaitInterval = configuration.WaitInterval;         
            QueueName = configuration.QueueName;
            ThreadCount = configuration.ControllerThreads;
            QueueManagerType = queueManagerType ?? QueueManagerType;
            ConnectionString = connectionString ?? ConnectionString;
        }

        /// <summary>
        /// Sets the types of messages that this controller is looking for
        /// </summary>
        public string QueueName { get; set; }


        /// <summary>
        /// Connection String for the database
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Determines whether the controller is processing messages
        /// </summary>
        protected virtual bool Active { get; set; }


        /// <summary>
        /// determines if the service is paused
        /// </summary>        
        public virtual bool Paused { get; set; }


        /// <summary>
        /// Determines how often the control checks for new messages
        /// Set in milliseconds.
        /// </summary>
        public virtual int WaitInterval { get; set; } = -1;

        /// <summary>
        /// Number of threads processing the queue
        /// </summary>
        public virtual int ThreadCount { get; set; }

        /// <summary>
        /// Counter that keeps track of how many messages have been processed 
        /// since the server started.
        /// </summary>

        [JsonIgnore]
        public virtual int MessagesProcessed { get; set; }

        
        /// <summary>
        /// The specific type of the message manager class
        /// </summary>        
        [JsonIgnore]
        public Type QueueManagerType { get; set; }        

        /// <summary>
        /// Max retries for failed requests.
        /// </summary>
        public int MaxRetries { get; set; } = 0;

        /// <summary>
        /// The Queue Controller Type to create an instance from.
        /// Used in the configuration to determine which type
        /// to instantiate
        /// </summary>
        public string QueueControllerType
        {
            get
            {
                if (string.IsNullOrEmpty(field))
                {
                    var type = GetType();
                    return type.FullName;
                }
                return field;
            }
            set;
        }

        /// <summary>
        /// Optional function you can hook to handle creation of the QueueManager
        /// instance. Use this to create and configure the QUeueManager instance
        /// </summary>
        [JsonIgnore]
        public Func<QueueMessageManager> OnCreateQueueManager { get; set; }

        [JsonIgnore]
        public ILogger LogManager { get; }

        /// <summary>
        /// Starts queue processing in the background and returns immediately.
        /// 
        /// It starts the controller asynchronously on the specified number of threads.
        /// Multiple threads are allowed to allow for simultanous processing of messages.
        /// 
        /// 
        /// This is a common scenario for Windows Forms interfaces so the UI
        /// stays active while the application monitors and processes the
        /// queue on a separate non-ui thread
        /// </summary>
        public virtual void StartProcessingAsync(int threads = -1)
        {
            if (!OnStartProcessing())
                return;

            // threads are marked as 0 - don't start any threads
            if (threads < 1 && ThreadCount < 1)
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
        /// Starts processing the controller's queue on the current thread.
        /// and runs until `Active` is set to false.
        ///         
        /// This method is meant to run on a non-UI thread as it will
        /// block and wait for messages to arrive in the queue.              
        /// </summary>        
        public virtual void StartProcessing()
        {
            Active = true;
            Paused = false;

            if (WaitInterval == -1)
                WaitInterval = DEFAULT_INTERVAL;        

            while (Active)
            {
                if (Paused)
                {
                    Thread.Sleep(WaitInterval);
                    continue;
                }
                
                QueueMessageManager manager;
                if (OnCreateQueueManager != null)
                    manager = OnCreateQueueManager.Invoke();
                else
                    manager = Activator.CreateInstance(QueueManagerType, [ ConnectionString ?? string.Empty ]) as QueueMessageManager;
                

                var config = QueueMessageManagerConfiguration.Current;
                if(config.AutoCreateTables)
                    manager.AutoCreateTables = true;
                               
                using (manager)
                {
                    if (OnGetNextQueueMessage(manager, QueueName) == null)                                        
                    {
                        if (!string.IsNullOrEmpty(manager.ErrorMessage))
                            OnNextMessageFailed(manager, new ApplicationException(manager.ErrorMessage));
                        
                        Thread.Sleep(WaitInterval);

                        continue;
                    }

                    // Fire events to execute the real operation
                    ExecuteSteps(manager).FireAndForget();
                }

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
        /// Pauses processing by keeping the thread alive
        /// and waiting until the pause is unset
        /// </summary>
        /// <param name="pause"></param>
        public virtual void PauseProcessing(bool pause = true)
        {
            Paused = pause;
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
        protected virtual async Task ExecuteSteps(QueueMessageManager manager)
        {
            try
            {
                // Hook up start processing
                await OnExecuteStartAsync(manager); 
                OnExecuteStart(manager);
                
                // Hookup end processing
                OnExecuteComplete(manager);
            }
            catch (Exception ex)
            {
                OnExecuteFailed(manager, ex);
            }

            MessagesProcessed++;
        }


        /// <summary>
        /// Event called when an individual request starts processing
        /// Your user code can attach to this event and start processing
        /// with the message information.
        /// </summary>        
        [JsonIgnore]
        public Action<QueueMessageManager> ExecuteStart;


        /// <summary>
        /// Event called when an individual request starts processing
        /// Your user code can attach to this event and start processing
        /// with the message information.
        /// </summary>        
        [JsonIgnore]
        public Func<QueueMessageManager, Task> ExecuteStartAsync;

       
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
            ExecuteStart?.Invoke(manager);
        }


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
        protected virtual Task OnExecuteStartAsync(QueueMessageManager manager)
        {
            if (ExecuteStartAsync == null) return Task.CompletedTask;
            return ExecuteStartAsync.Invoke(manager);
        }


        /// <summary>
        /// Event fired when the asynch operation has successfully completed
        /// </summary>
        [JsonIgnore]
        public Action<QueueMessageManager> ExecuteComplete = null;


        /// <summary>
        /// Override this method to do any post processing that needs to happen
        /// after each async operation has successfully completed. Optional - use
        /// for things like logging or reporting on status.
        /// </summary>
        /// <param name="manager">
        /// QueueManager instance. Use its Item property to get access to the current method
        /// </param>
        protected virtual void OnExecuteComplete(QueueMessageManager manager)
        {
            ExecuteComplete?.Invoke(manager);
        }

        /// <summary>
        /// Event fired when the asynch operation has failed to complete (an exception
        /// was thrown during processing). Implement for logging or notifications.
        /// </summary>
        [JsonIgnore] 
        public Action<QueueMessageManager, Exception> ExecuteFailed;

        
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
            else
            {
                manager.FailRequest(messageText: ex.Message, autoSave: true);                
            }
        }

        /// <summary>
        /// Message hook that's responsible for retrieving the next message.
        /// The base version pulls the next message for the given queue.    
        /// You can override this method to conditionally override this 
        /// behavior such as filter when and how messages are read.
        /// </summary>
        /// <param name="manager">A manager instance that can retrieve</param>
        /// <param name="queueName">The queue to check</param>
        /// <returns></returns>
        protected virtual QueueMessageItem OnGetNextQueueMessage(QueueMessageManager manager, string queueName)
        {            
            return manager.GetNextQueueMessage(queueName);
        }

        /// <summary>
        /// Event fired when the read operation to retrieve the next message from
        /// the database has failed. Allows for error handling or logging.
        /// </summary>
        [JsonIgnore]
        public  Action<QueueMessageManager, Exception> NextMessageFailed;


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


        public virtual void Dispose()
        {
            StopProcessing();
        }

        public override string ToString()
        {
            return $"{QueueName} [ {ThreadCount} thread(s), {WaitInterval} ms, paused: {Paused} ]";
        }

        /// <summary>
        /// Creates a new controller instance from the ControllerType stored 
        /// on this class. Used internally to create a new controller instance
        /// when starting up from configuration.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public QueueMessageManager CreateNewControllerInstance(string typeName = null)
        {
            if (string.IsNullOrEmpty(typeName))
                typeName = this.QueueControllerType;

            QueueMessageManager manager;
            if (OnCreateQueueManager != null)
                manager = OnCreateQueueManager.Invoke();
            else
                manager = Activator.CreateInstance(QueueManagerType, [ConnectionString ?? string.Empty]) as QueueMessageManager;


            return manager;

        }
    }
}
