using System;
using System.Threading;
using Westwind.MessageQueueing;

namespace Westwind.MessageQueueing.WebHost
{
    public class QueueMonitorMultipleQueueController : QueueControllerMultiple
    {
        protected override void OnExecuteStart(QueueMessageManager manager)
        {
            // write a starting message to QueueMonitor
            manager.Item.TextInput = DateTime.UtcNow.ToString("u");
            QueueMonitorServiceHub.WriteMessage(manager.Item);
            
            base.OnExecuteStart(manager);
            
            var queueItem = manager.Item;
            bool result;

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
                    case "HELLOWORLD":
                    {
                        Thread.Sleep(2000);
                        manager.CompleteRequest(messageText: queueItem.Message + " -  completed at " + DateTime.Now, autoSave: true);
                        break;
                    }

                    default:
                        // TODO: Remove for production   Random wait for 1-500ms                                           
                        //Thread.Sleep( (int) (DateTime.Now.Ticks % 500));

                        // All requests that get picked up by the queue get their started properties set,
                        // so we MUST mark them complete, even if we did not have any local action code here,
                        // because we cannot leave them in the half-way complete state.
                        if (
                            !manager.CompleteRequest(messageText: "Processing complete. Action not supported.",
                                autoSave: true))
                        {
                            // this is pointless - if this save fails it's likely the save you are doing in
                            // onError will also fail
                            OnError(manager);
                            return;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                var ex2 = ex.GetBaseException();
                OnError(manager, ex2.Message, ex2);
                //+ "\r\n" + ex.Source + "\r\n" + ex.StackTrace);                
            }

        }

        protected override void OnExecuteComplete(QueueMessageManager manager)
        {
            base.OnExecuteComplete(manager);

            // Get Starting timestamp from TextInput
            int elapsed = 0;
            DateTime time = DateTime.UtcNow;
            if (DateTime.TryParse(manager.Item.TextInput, out time))
                elapsed = (int) DateTime.UtcNow.Subtract(time.ToUniversalTime()).TotalMilliseconds;

            // also send down a waiting message count
            int waitingMessages =  GetWaitingMessageCount(manager);

            WriteMessage(manager.Item, elapsed, waitingMessages);
        }

        protected override void OnExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            //timestamp from text input
            int elapsed = 0;
            DateTime time = DateTime.UtcNow;
            if (DateTime.TryParse(manager.Item.TextInput, out time))
                elapsed = (int) DateTime.UtcNow.Subtract(time.ToUniversalTime()).TotalMilliseconds;

            var qm = manager.Item;
            int waitingMessages = GetWaitingMessageCount(manager);

            WriteMessage(qm, elapsed, waitingMessages);

            base.OnExecuteFailed(manager, ex);
        }

        protected void OnError(QueueMessageManager manager, string message = null, Exception ex = null)
        {
            if (message == null)
                message = manager.ErrorMessage;

            manager.CancelRequest(messageText: "Queue Error: " + message);
            manager.Save();


            string details = null;
            if (ex != null)
                details = ex.Source + "\r\n" + ex.StackTrace;

            //LogManager.Current.LogError(message, details);
            // send email
            //AppUtils.SendAdminEmail("MPWFQMM Failure", errorMessage);            
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

         int GetWaitingMessageCount(QueueMessageManager manager, int delay = 10)
        {
            return manager.GetWaitingQueueMessageCount(QueueName);

            //int count = 0;
            //if (WaitingQueueMessageCountLastAccess < DateTime.UtcNow.AddSeconds(delay * -1))
            //{
            //    lock (WaitingQueueMessageCountLock)
            //    {
            //        if (WaitingQueueMessageCountLastAccess < DateTime.UtcNow.AddSeconds(delay *-1))
            //        {
            //            WaitingQueueMessageCountLastAccess = DateTime.UtcNow;
            //            count = manager.GetWaitingQueueMessageCount(QueueName);
            //        }
            //    }                
            //}

            //return count;
        }

    }
}