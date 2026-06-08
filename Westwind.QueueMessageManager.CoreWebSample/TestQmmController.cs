using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using System.Diagnostics;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.Utilities;

namespace Westwind.QueueMessageManager.CoreWebSample;

public class TestQmmController : QueueControllerMultiple
{

    public TestQmmController(QueueMessageManagerConfiguration config = null,
        string connectionString = null, IEnumerable<QueueController> controllers = null,
        Type managerType = null) : base(config, connectionString, controllers, managerType)
    { 

    }


    protected override void OnExecuteStart(MessageQueueing.QueueMessageManager manager)
    {
        int x = 1;
    }

    protected override async Task OnExecuteStartAsync(MessageQueueing.QueueMessageManager manager)
    {
        await base.OnExecuteStartAsync(manager);

        var item = manager.Item;
        var swatch = Stopwatch.StartNew();

        // TEST ONLY
        await Task.Delay(1000); // so we can see submission

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

                manager.CompleteRequest();
                manager.Save();
            }
            else
            {
                await Task.Delay(1200);
                manager.FailRequest(messageText: "Unknown action: " + item.Action);
                manager.Save();
            }
        }
        catch (Exception ex)
        {
            manager.FailRequest(messageText: $"Processing failed: " + ex.GetBaseException().Message);
            manager.Save();
        }

        swatch.Stop();
        QueueMonitorServiceHub.WriteMessageInternal(item, elapsed: (int)swatch.ElapsedMilliseconds).FireAndForget();        
    }
}