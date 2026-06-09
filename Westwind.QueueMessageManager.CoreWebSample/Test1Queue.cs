// #define USE_ASYNC

using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.Utilities;



namespace Westwind.QueueMessageManager.CoreWebSample;

public class Test1Queue : QueueController
{
    public Test1Queue() 
    {
        WaitInterval = 1000;
        QueueName = "Test1";
    }


    protected override void OnExecuteStart(MessageQueueing.QueueMessageManager manager)
    {
#if !USE_ASYNC        

        var item = manager.Item;

        if (item == null)
            return;

        // Testing only brief delay so we can see transition from Submitted to Started
        Thread.Sleep(1000);

        try
        {
            switch (item.Action)
            {

                case "PRINT":
                {
                    item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                    manager.StartRequest();
                    manager.Save();

                    QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();

                    Thread.Sleep(3000);
                    item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;

                    OnExecuteComplete(manager);
                    break;
                }
                default:
                {
                    manager.CancelRequest(messageText: "Unknown action: " + item.Action + "\nOriginal message:\n" + item.Message);
                    manager.Save();

                    // no handler so directly write out
                    WriteMessageHub(manager.Item);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            OnExecuteFailed(manager, ex);
        }
#endif
    }
    

    protected override async Task OnExecuteStartAsync(MessageQueueing.QueueMessageManager manager)
    {
#if USE_ASYNC
        var item = manager.Item;

        if (item == null)
            return;

        // Testing only brief delay so we can see transition from Submitted to Started
        await Task.Delay(1000);

        try
        {
            switch (item.Action)
            {

                case "PRINT":
                {
                    item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                    manager.StartRequest();
                    manager.Save();

                    QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();

                    Thread.Sleep(3000);
                    item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;

                    OnExecuteComplete(manager);
                    break;
                }
                default:
                {
                    manager.CancelRequest(messageText: "Unknown action: " + item.Action + "\nOriginal message:\n" + item.Message);
                    manager.Save();

                    // no handler so directly write out
                    WriteMessageHub(manager.Item);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            OnExecuteFailed(manager, ex);
        }
#endif
    }

    protected override void OnExecuteComplete(MessageQueueing.QueueMessageManager manager)
    {
        manager.CompleteRequest();
        manager.Save();

        WriteMessageHub(manager.Item);
    }

    protected override void OnExecuteFailed(MessageQueueing.QueueMessageManager manager, Exception ex)
    {        
        manager.FailRequest(messageText: $"Request failed: {ex.Message}\nOriginal message:\n{manager.Item.Message}");
        manager.Save();

        WriteMessageHub(manager.Item);
    }


    protected void WriteMessageHub(QueueMessageItem item, string messageText = null)
    {        
        if (!string.IsNullOrEmpty(messageText))
            item.Message = messageText;
        

        QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();
    }
}