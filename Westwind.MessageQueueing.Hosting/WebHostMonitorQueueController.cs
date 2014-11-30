using System;
using System.Threading;
using Westwind.MessageQueueing;

namespace Westwind.MessageQueueing.Hosting
{
    /// <summary>
    /// This class is a multiple controller implementation
    /// that supports the QueueMonitor for messaging output
    /// when request processing.
    /// </summary>
    public class WebHostMonitorQueueController : QueueControllerMultiple
    {
        /// <summary>
        /// Implement OnExecuteStart to run queued actions/operations.
        /// Call manager.CompleteRequest() or manager.CancelRequest() to
        /// complete queue items.
        /// 
        /// Below is a commented simple example
        /// </summary>
        /// <param name="manager"></param>
        //protected override void OnExecuteStart(QueueMessageManager manager)
        //{
        //    base.OnExecuteStart(manager);            
        //
        //    string action = manager.Item.Action;
        //    try
        //    {
        //        switch (action)
        //        {
        //            case "HelloWorld":
        //            {
        //                Thread.Sleep(2000);
        //                manager.CompleteRequest(messageText: queueItem.Message + " - NEW completed at " + DateTime.Now,
        //                    autoSave: true);
        //                break;
        //            }
        //            default:
        //            {
        //                manager.CancelRequest(messageText: "Failed: No matching action", autoSave: true);
        //                break;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        manager.CancelRequest(messageText: "Failed: " + ex.Message, autoSave: true);
        //    }
        //}

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
            base.OnExecuteFailed(manager, ex);

            //timestamp from text input
            int elapsed = 0;
            DateTime time = DateTime.UtcNow;
            if (DateTime.TryParse(manager.Item.TextInput, out time))
                elapsed = (int) DateTime.UtcNow.Subtract(time.ToUniversalTime()).TotalMilliseconds;

            var qm = manager.Item;
            int waitingMessages = GetWaitingMessageCount(manager);

            WriteMessage(qm, elapsed, waitingMessages);
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
        }

    }
}