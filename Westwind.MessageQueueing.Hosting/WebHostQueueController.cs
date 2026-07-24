using System;
using System.Threading.Tasks;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Hosting;


/// <summary>
/// This is a base controller that provides base messages for OnExecuteComplete 
/// messages the QueueMonitorServiceHub when messages are completed
/// </summary>
public class WebHostQueueController : QueueController
{
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
    protected override async Task ExecuteSteps(QueueMessageManager manager)
    {
        try
        {
            WriteMessageHub(manager.Item);  // refresh SignalR
            QueueMonitorServiceHub.GetWaitingQueueMessageCountInternal(manager.Item?.QueueName).FireAndForget();

            // Hook up start processing

            // Async logic
            await OnExecuteStartAsync(manager);

            // Sync logic
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

    #region SignalR Notifications

    /// <summary>
    /// Writes a message to the SignalR hub for an updated QueueItem at the top of the list
    /// </summary>
    /// <param name="item"></param>
    /// <param name="messageText"></param>
    protected virtual void WriteMessageHub(QueueMessageItem item, string messageText = null)
    {
        if (!string.IsNullOrEmpty(messageText))
            item.Message = messageText;

        QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();
    }

    /// <summary>
    /// Writes a message to the SignalR hub as a status bar message
    /// </summary>
    /// <param name="item"></param>
    /// <param name="messageText"></param>
    protected virtual void StatusMessageHub(string messageText = null)
    {
        QueueMonitorServiceHub.StatusMessage(messageText).FireAndForget();
    }

    #endregion

}