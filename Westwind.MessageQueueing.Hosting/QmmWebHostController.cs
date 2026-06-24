using System;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Hosting
{

    /// <summary>
    /// This is a base controller that provides base messages for OnExecuteComplete 
    /// messages the QueueMonitorServiceHub when messages are completed
    /// </summary>
    public class QmmWebHostController : QueueController
    {


        protected override void OnExecuteComplete(MessageQueueing.QueueMessageManager manager)
        {
            if (!manager.MessageHandled)
            {
                manager.CompleteRequest();
                manager.Save();

                WriteMessageHub(manager.Item);
            }
        }

        protected override void OnExecuteFailed(MessageQueueing.QueueMessageManager manager, Exception ex)
        {
            if (!manager.MessageHandled)
            {
                manager.FailRequest(messageText: $"Request failed: {ex.Message}\nOriginal message:\n{manager.Item.Message}");
                manager.Save();

                WriteMessageHub(manager.Item);
            }
        }


        /// <summary>
        /// Writes a message to the SignalR hug for the QueueItem
        /// </summary>
        /// <param name="item"></param>
        /// <param name="messageText"></param>
        protected virtual void WriteMessageHub(QueueMessageItem item, string messageText = null)
        {
            if (!string.IsNullOrEmpty(messageText))
                item.Message = messageText;

            QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();
        }
    }



}