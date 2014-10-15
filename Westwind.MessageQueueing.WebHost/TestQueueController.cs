using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace Westwind.MessageQueueing.WebHost
{
    public class TestQueueController : QueueMonitorMultipleQueueController
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
                            manager.CompleteRequest(messageText: queueItem.Message + " - NEW completed at " + DateTime.Now, autoSave: true);
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
    }
}