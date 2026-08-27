//#define USE_ASYNC

using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;

namespace Westwind.QueueMessageManager.CoreWebSample;

/// <summary>
/// Sample Web Queue Controller 
/// </summary>
public class Test2Queue : WebHostQueueController
{
    public Test2Queue()
    {
        WaitInterval = 1000;
        QueueName = "Test2";
        ConnectionString = qmmApp.ConnectionString;
    }

// You can use either sync or async versions of OnExecuteStart/Async or
// you can use both (with different actions).
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
                item.PercentComplete = 10;
                
                // manager.StartRequest();     // item is already started by the Web Host QueueController
                manager.Save();
                WriteMessageHub(manager.Item);

                Thread.Sleep(1200);                
                manager.ProgressRequest(percentComplete: 30, messageText: "Processing PrintTest2... (30%)");
                manager.Save();
                WriteMessageHub(manager.Item);


                Thread.Sleep(1200);
                manager.ProgressRequest(percentComplete: 60, messageText: "Processing PrintTest2... (60%)");
                manager.Save();
                WriteMessageHub(manager.Item);

                Thread.Sleep(1200);
                // explicit assignment
                item.Message = "Processing PrintTest2...";
                item.PercentComplete = 90;
                manager.Save();
                WriteMessageHub(manager.Item);

                Thread.Sleep(1000);            
                manager.CompleteRequest(messageText: "Completed on: " + DateTime.Now + " - Processing complete - Thread: " + Thread.CurrentThread.ManagedThreadId);                                            

                // manager.MessageHandled = true;

                break;
            default:
                throw new InvalidOperationException("Invalid verb: " + item.Action);
        }

    }


}