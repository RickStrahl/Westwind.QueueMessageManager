using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.Hosting;
using Westwind.Utilities;

namespace Westwind.QueueMessageManager.CoreWebSample;

public class Test2Queue : QmmWebHostController
{
    public Test2Queue()
    {
        WaitInterval = 1000;
        QueueName = "Test2";
        ConnectionString = qmmApp.ConnectionString;
    }


    protected override void OnExecuteStart(MessageQueueing.QueueMessageManager manager)
    {
        var item = manager.Item;

        if (item == null)
            return;

        // Testing only brief delay so we can see transition from Submitted to Started
        Thread.Sleep(1000);



        switch (item.Action)
        {
            case "PRINTTEST2":
                item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                manager.StartRequest();
                manager.Save();
                WriteMessageHub(manager.Item);


                Thread.Sleep(3000);
                
                manager.CompleteRequest(messageText: "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId);
                //manager.Save();
                //WriteMessageHub(manager.Item);
                // manager.MessageHandled = true;

                break;
            default:
                throw new InvalidOperationException("Invalid verb: " + item.Action);
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


                await Task.Delay(3000);
                item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                manager.CompleteRequest();
                manager.Save();
            }
        }
        catch (Exception ex)
        {
            manager.FailRequest(messageText: $"Request failed: {ex.Message}\nOriginal message:\n{item.Message}");
            manager.Save();
        }
    }

   

}