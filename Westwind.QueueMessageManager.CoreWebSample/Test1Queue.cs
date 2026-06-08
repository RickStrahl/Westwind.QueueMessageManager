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
        //return; // sync

        var item = manager.Item;

        if (item == null)
            return;

        // Testing only brief delay so we can see transition from Submitted to Started
        Thread.Sleep(1000);

        try
        {
            if (item.Action == "PRINT")
            {

                item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                manager.StartRequest();
                manager.Save();
                
                QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();


                Thread.Sleep(3000);
                item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;

                OnExecuteComplete(manager);
            }
        }
        catch (Exception ex)
        {
            OnExecuteFailed(manager, ex);
        }
    }

    


    protected override async Task OnExecuteStartAsync(MessageQueueing.QueueMessageManager manager)
    {
        return; // no async

        var item = manager.Item;

        if (item == null)
            return;

        // Testing only brief delay so we can see transition from Submitted to Started
        await Task.Delay(1000);

        try
        {
            if (item.Action == "PRINT")
            {

                item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                manager.StartRequest();
                manager.Save();

                QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();


                await Task.Delay(3000);
                item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;

                OnExecuteComplete(manager);                
            }
        }
        catch (Exception ex)
        {
            OnExecuteFailed(manager, ex);
        }
    }

    protected override void OnExecuteComplete(MessageQueueing.QueueMessageManager manager)
    {
        manager.CompleteRequest();
        manager.Save();

        QueueMonitorServiceHub.WriteMessageInternal(manager.Item).FireAndForget();
    }

    protected override void OnExecuteFailed(MessageQueueing.QueueMessageManager manager, Exception ex)
    {
        manager.FailRequest(messageText: $"Request failed: {ex.Message}\nOriginal message:\n{manager.Item.Message}");
        manager.Save();

        QueueMonitorServiceHub.WriteMessageInternal(manager.Item).FireAndForget();
    }
}