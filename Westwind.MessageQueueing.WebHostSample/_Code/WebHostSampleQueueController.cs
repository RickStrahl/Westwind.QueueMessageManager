using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using Westwind.MessageQueueing.Hosting;

namespace Westwind.MessageQueueing.WebHostSample
{

    /// <summary>
    /// This is a minimal sample QueueCOntroller implementation that handles
    /// one simple HELLOWORLD action operation. 
    /// </summary>
    public class WebHostSampleQueueController : WebHostMonitorQueueController
    {
        protected override void OnExecuteStart(QueueMessageManager manager)
        {
            // write a 'request starting' message to QueueMonitor
            manager.Item.TextInput = DateTime.UtcNow.ToString("u");
            QueueMonitorServiceHub.WriteMessage(manager.Item);

            // always call base to allow any explicit events to also process
            base.OnExecuteStart(manager);

            var queueItem = manager.Item;

            try
            {
                // Action can be used as a routing mechanism 
                // to determine which operation to perfom
                string action = queueItem.Action;

                switch (action)
                {
                    case "HELLOWORLD":
                    {
                        // call whatever long running operations you need to run
                        Thread.Sleep(2000);

                        // always either complete or cancel the request
                        manager.CompleteRequest(messageText: queueItem.Message +
                                                             " - HELLOWORLD completed at " + DateTime.Now,
                            autoSave: true);
                        break;                    
                    }
                    case "NEWXMLORDER":
                    {
                        // call whatever long running operations you need to run
                        Thread.Sleep(2000);

                        // always either complete or cancel the request
                        manager.CompleteRequest(messageText: queueItem.Message +
                                                             " - NEWXMLORDER completed at " + DateTime.Now,
                            autoSave: true);
                        break;
                    }
                    case "MPWF":
                    {
                        Thread.Sleep(1000);

                            // always either complete or cancel the request
                            manager.CompleteRequest(messageText: queueItem.Message +
                                                                 " - completed at " + DateTime.Now,
                                autoSave: true);
                            break;
                        }

                    case "GOBIG":
                    {
                        // call whatever long running operations you need to run
                        Thread.Sleep(4000);

                        // always either complete or cancel the request
                        manager.CompleteRequest(messageText: queueItem.Message +
                                                             " - GO BIG OR GO HOME completed at " + DateTime.Now,
                            autoSave: true);
                        break;
                    }

                    default:
                        // All requests that get picked up by the queue get their started properties set,
                        // so we MUST mark them complete, even if we did not have any local action code here,
                        // because we cannot leave them in the half-way complete state.
                        manager.CancelRequest(messageText: "Processing failed - action not supported: " + action,
                            autoSave: true);
                        break;
                }
            }
            catch (Exception ex)
            {
                var ex2 = ex.GetBaseException();

                // route to OnError (base) which logs error
                // and cancels the request
                OnError(manager, ex2.Message, ex2);                
            }
        }
    }
}