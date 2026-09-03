
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.MessageQueueing.MongoDb;


namespace Westwind.QueueMessageManager.CoreWebSample;

/// <summary>
/// Sample Web Queue Controller 
/// </summary>
public class Test1Queue :  WebHostQueueController
{

    public Test1Queue() 
    {
        WaitInterval = 1000;
        QueueName = "Test1";
        
        //QueueManagerType = typeof(QueueMessageManagerSql);
        QueueManagerType = typeof(QueueMessageManagerMongoDb);
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
            case "PRINT":
                {
                    item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                    manager.StartRequest();
                    manager.Save();

                    Thread.Sleep(3000); // simulat work
                    item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                    manager.CompleteRequest();

                    // This is not needed because the default OnExecuteComplete() finishes out the queue request/message
                    // unless manager.MessageHandled = true;

                    // manager.Save();
                    // WriteMessageHub(manager.Item);
                    // manager.MessageHandled = true;  // explicitly close out the queue request, OnExecuteComplete() not called

                    break;
                }
            case "EMAIL":
            {
                item.Message = "Started on: " + DateTime.Now + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
                manager.StartRequest();
                manager.Save();

                Thread.Sleep(3000); // simulat work                
                manager.CompleteRequest(messageText: "Completed on: " + DateTime.Now + " - Thread: " + Thread.CurrentThread.ManagedThreadId);  // Save() handled by OnExecuteComplete() 

                break;
            }
            default:
                {
                    // force exception so it fails
                    throw new InvalidOperationException("Unknown action: " + item.Action + "\nOriginal message:\n" + item.Message);
                }
        }

    }


    protected override async Task OnExecuteStartAsync(MessageQueueing.QueueMessageManager manager)
    {
        var item = manager.Item;

        if (item == null)
            return;

        // Testing only brief delay so we can see transition from Submitted to Started
        await Task.Delay(1000);

        if (item.Action == "PRINTASYNC")
        {

            item.Message = "Print Async Started on: " + DateTime.Now + " -  Thread: " + Thread.CurrentThread.ManagedThreadId;
            item.PercentComplete = 10;
            manager.StartRequest();
            manager.Save();
            WriteMessageHub(manager.Item);  // notify Monitor

            await Task.Delay(1500);
            manager.ProgressRequest(percentComplete: 40, messageText: "Processing PrintAsync... (40%)");
            WriteMessageHub(manager.Item);

            await Task.Delay(1500);
            manager.ProgressRequest(percentComplete: 80, messageText: "Processing PrintAsync... (80%)");
            WriteMessageHub(manager.Item);

            await Task.Delay(3000);
            
            // Completion message is assigned
            item.Message = "Print Async Completed on: " + DateTime.Now + " - Thread: " + Thread.CurrentThread.ManagedThreadId;

            // Async operations should mark requests as completed (or failed/cancelled) to avoid OnExecuteComplete() being called after the async method completes.
            manager.CompleteRequest();   // at minimum mark request complete

            // This can still be handled by the 
            //manager.Save();
            //WriteMessageHub(manager.Item);
            //manager.MessageHandled = true;  // explicitly close out the queue request, OnExecuteComplete() not called
        }

        // Default class behavior: 
        // -----------------------
        // OnExecuteComplete() is fired after this method unless manager.MessageHandled = true;
        // OnExecuteFailed() on an exception unless manager.MessageHandled = true;
        //
        // Both methods Save the request with the appropriate status settings.
        // Point: Throw exceptions for failed queue operations
    }

        

    //protected override void OnExecuteComplete(MessageQueueing.QueueMessageManager manager)
    //{
    //    manager.CompleteRequest();
    //    manager.Save();

    //    WriteMessageHub(manager.Item);
    //}

    //protected override void OnExecuteFailed(MessageQueueing.QueueMessageManager manager, Exception ex)
    //{        
    //    manager.FailRequest(messageText: $"Request failed: {ex.Message}\nOriginal message:\n{manager.Item.Message}");
    //    manager.Save();

    //    WriteMessageHub(manager.Item);
    //}


    //protected void WriteMessageHub(QueueMessageItem item, string messageText = null)
    //{        
    //    if (!string.IsNullOrEmpty(messageText))
    //        item.Message = messageText;


    //    QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();
    //}
}