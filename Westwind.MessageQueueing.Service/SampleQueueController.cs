using System;
using System.Linq;
using Westwind.Utilities;
using System.Threading;
using System.Diagnostics;
using Westwind.Utilities.Logging;
using Westwind.MessageQueueing;

namespace Westwind.MessageQueueing.Service
{
    public class SampleQueueController : QueueController
    {
        private const string STR_STARTTIME_KEY = "_QMMC_StartTime";
        
        
        public SampleQueueController()         
        { }
  

        protected override void OnExecuteStart(QueueMessageManager manager)
        {
            base.OnExecuteStart(manager);            

            var queueItem = manager.Entity;

            try
            {
                string action = queueItem.Action;

                if (!string.IsNullOrEmpty(action))
                {
                    //Initialize Anything
                    action = action.Trim();
                }

                switch (action)
                {
                    default:
                        // TODO: Remove for production                                                
                        Thread.Sleep( (int) (DateTime.Now.Ticks % 500));

                        // use this instead to ensure that messages get updated properly and consistently
                        // that is: All flags are set, date is always UTC date, etc.
                        //if (!manager.CancelRequest(messageText: "Unknown Action", autoSave: true))
                        if (!manager.CompleteRequest(messageText: "Processing complete.", autoSave: true))
                        {
                            // this is pointless - if this save fails
                            // it's likely the save you are doing in
                            // onError will also fail
                            OnError(manager);
                            return;
                        }
                        //manager.CompleteCancelRequest(messageText: "Invalid message action provided.");
                        break;
                }
            }
            catch (Exception ex)
            {
                OnError(manager,ex.GetBaseException().Message);
                return;
            }

        }

        private bool PrintImage()
        {
            throw new NotImplementedException();
        }

        
        protected void OnError(QueueMessageManager manager, string message = null)
        {
            if (message == null)
                message = manager.ErrorMessage;

            // clear out the message
            // don't do this
            manager.CancelRequest(messageText: "Failed: " + message,autoSave: true);

            LogManager.Current.LogError(message);
            // send email
            //AppUtils.SendAdminEmail("MPWFQMM Failure", errorMessage);            
        }
        
        protected override void ExecuteSteps(QueueMessageManager manager)
        {
            // show a started message
            Stopwatch watch = new Stopwatch();
            watch.Start();

            // store on a thread
            LocalDataStoreSlot threadData = Thread.GetNamedDataSlot(STR_STARTTIME_KEY);
            Thread.SetData(threadData, watch);
            
            QueueMonitorServiceHub.WriteMessage(manager.Entity);

            base.ExecuteSteps(manager);
        }

        protected override void OnExecuteComplete(QueueMessageManager manager)
        {          
            base.OnExecuteComplete(manager);

            LocalDataStoreSlot threadData = Thread.GetNamedDataSlot(STR_STARTTIME_KEY);
            
            var watch = Thread.GetData(threadData);

            int elapsed = 0;
            if (watch != null)
            {
                ((Stopwatch)watch).Stop();
                elapsed = (int) ((Stopwatch)watch).ElapsedMilliseconds;
            }
            watch = null;
            Thread.SetData(threadData, watch);

            int waitingMessages = manager.GetWaitingQueueMessageCount(QueueName);
            
            WriteMessage(manager.Entity,elapsed,waitingMessages);
        }

        protected override void OnExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            var qm = manager.Entity;
            WriteMessage(qm);
            base.OnExecuteFailed(manager, ex);
        }

        protected override bool OnStartProcessing()
        {
            WriteMessage("Controller started with " + this.ThreadCount.ToString() + " threads.");
            return true;
        }

        protected override bool OnStopProcessing()
        {            
            WriteMessage("Controller stopped.");
            return true;
        }

        /// <summary>
        /// Writes out a message to the SignalR hub
        /// </summary>
        /// <param name="message"></param>
        public virtual void WriteMessage(string message)
        {
            // forward to SignalR Hub broadcast
            QueueMonitorServiceHub.WriteMessage(message);
        }

        /// <summary>
        /// Writes out a message to the SignalR hub
        /// </summary>
        /// <param name="queueItem"></param>
        /// <param name="elapsed"></param>
        /// <param name="waiting"></param>
        public virtual void WriteMessage(QueueMessageItem queueItem, int elapsed = 0, int waiting = -1)
        {
            // forward to SignalR Hub broadcast
            QueueMonitorServiceHub.WriteMessage(queueItem, elapsed, waiting);
        }

        public override void Dispose()
        {
            base.Dispose();
        }
        //private void UpdateQueueMessageStatus(QueueMessageManager manager, string status = null, string messageText = null, int percentComplete = -1)
        //{
        //    if (!string.IsNullOrEmpty(status))
        //        manager.Entity.Status = status;

        //    if (!string.IsNullOrEmpty(messageText))
        //        manager.Entity.Message = messageText;

        //    if (manager.Entity.PercentComplete > -1)
        //        manager.Entity.PercentComplete = percentComplete;
                     
        //    manager.Save();            
        //}
    }
}
