using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Westwind.MessageQueueing;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Hosting
{
    //[QueueAuthorize]
    public class QueueMonitorServiceHub : Hub
    {
        public QueueMonitorServiceHub()
        {            
        }

        /// <summary>
        /// Writes a message to the client that displays on the status bar
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allClients"></param>
        public async Task StatusMessage(string message, bool allClients = false)
        {
            if (allClients)
                await Clients.All.SendAsync("statusMessage", message);
            else
                await Clients.Caller.SendAsync("statusMessage", message);
        }

        /// <summary>
        /// Starts the service
        /// </summary>
        public async Task StartService()
        {
            // unpause the QueueController to start processing again
            Globals.Controller.PauseProcessing(false);

            await Clients.All.SendAsync("startServiceCallback", true);

            await Clients.All.SendAsync("writeMessage",
                "Queues starting with " + Globals.Controller.ThreadCount.ToString() + " threads.",
                "Info", DateTime.Now.ToString("HH:mm:ss"));
        }

        public async Task StopService()
        {
            // Pause - we can't stop service because that'll exit the server            
            Globals.Controller.PauseProcessing(true);

            await Clients.All.SendAsync("stopServiceCallback", true);

            await Clients.All.SendAsync("writeMessage", "Queue has been stopped.", "Info",
                DateTime.Now.ToString("HH:mm:ss"));
        }


        /// <summary>
        /// Gets the intial set of messages to be displayed by the QueueManager
        /// </summary>
        public async Task GetInitialMessages(string queueName = null)
        {
            if (string.IsNullOrEmpty(queueName))
                queueName = null;

            var queue = new QueueMessageManagerSql();
            List<QueueMessageItem> msgs = queue.GetRecentQueueItems(queueName, 10).Reverse().ToList();

            if (msgs.Count < 1)
            {
                if (!string.IsNullOrEmpty(queue.ErrorMessage))
                    throw new ApplicationException("Message retrieval failed: " + queue.ErrorMessage);

                // no msgs = just clear the list
                await Clients.All.SendAsync("ClearMessages");
            }


            foreach (var msg in msgs)
            {
                int elapsed = 0;
                DateTime time = DateTime.UtcNow;
                if (msg.Completed != null)
                {
                    if (msg.Started != null)
                        elapsed = (int)(msg.Completed.Value - msg.Started.Value).TotalMilliseconds;
                    time = msg.Completed.Value;
                }
                else if (msg.Started != null)
                {
                    time = msg.Started.Value;
                }

                WriteMessage(msg, elapsed, -1, time);
            }
        }

        public async Task getQueueNames()
        {
            var queues = new List<string>();
            foreach (var controller in Globals.Controller.Controllers)
            {
                queues.Add(controller.QueueName);
            }

            await Clients.Caller.SendAsync("getQueueNamesCallback", queues);
        }

        public async Task getQueueMessage(string id)
        {
            var queue = new QueueMessageManagerSql();
            var qitem = queue.Load(id);
            await Clients.Caller.SendAsync("getQueueMessageCallback", qitem);
        }

        public async Task GetServiceStatus(string queueName)
        {
            var controller = Globals.Controller;
            if (controller.Controllers == null || string.IsNullOrEmpty(queueName))
                controller = null;

            if (controller == null)
            {
                await Clients.Caller.SendAsync("getServiceStatusCallback", null);
                return;
            }

            var inst = controller.Controllers
                .FirstOrDefault(ctl => ctl.QueueName == queueName);


            if (inst == null)
                await Clients.Caller.SendAsync("getServiceStatusCallback", null);

            await Clients.Caller.SendAsync("getServiceStatusCallback",
                new QueueControllerStatus()
                {
                    queueName = inst.QueueName,
                    waitInterval = inst.WaitInterval,
                    threadCount = inst.ThreadCount,
                    paused = inst.Paused
                });
        }


        public async Task UpdateServiceStatus(QueueControllerStatus status)
        {
            if (status == null)
            {
                await Clients.Caller.SendAsync("updateServiceStatus", null);
                return;
            }

            var controller = Globals.Controller.Controllers
                .FirstOrDefault(ct => ct.QueueName == status.queueName);

            if (controller == null)
                return;

            controller.WaitInterval = status.waitInterval;
            controller.QueueName = status.queueName;

            // Max 50 threads
            controller.ThreadCount = status.threadCount;

            if (controller.ThreadCount > 50)
            {
                controller.ThreadCount = 50;
                status.threadCount = 50;
            }

            var config = QueueMessageManagerConfiguration.Current;

            // grab the individual controller
            var controllerConfig = config.Controllers
                .FirstOrDefault(ct => ct.QueueName == status.queueName);

            if (config == null)
                return;

            controllerConfig.ControllerThreads = controller.ThreadCount;
            controllerConfig.WaitInterval = controller.WaitInterval;
            controllerConfig.QueueName = status.queueName;

            // try to save config settings
            //config.Write();
            Task.Delay(2000).ContinueWith(x => config.Write());

            controller.StopProcessing();
            controller.StartProcessingAsync();

            StatusMessage("Service Status settings updated.", true);

            // update all clients with the status information
            await Clients.All.SendAsync("updateControllerStatusCallback", status);
        }

        /// <summary>
        /// Returns a count of messages that is waiting for a given queue or
        /// all queues
        /// </summary>
        /// <param name="queueName"></param>
        public async Task GetWaitingQueueMessageCount(string queueName = null)
        {
            if (string.IsNullOrEmpty(queueName))
                queueName = null; // force all

            using (var manager = new QueueMessageManagerSql())
            {
                int count = manager.GetWaitingQueueMessageCount(queueName);
                // broadcast to all clients
                await Clients.All.SendAsync("getWaitingQueueMessageCountCallback", count);
            }
        }


        public async Task Notify(QueueMessageItem queueItem, int elapsed = 0, int waiting = 0)
        {
            await WriteMessage(queueItem, elapsed, waiting);
        }


        /// 
        /// *** Client Broadcast Services
        /// 
        /// <summary>
        /// Context instance to access client connections to broadcast to
        /// </summary>
        public static IHubContext<QueueMonitorServiceHub> HubContext
        {
            get
            {
                if (_context == null)
                    throw new ApplicationException(
                        "HubContext is not initialized. Set it during Start up with:\nQueueMonitorServiceHub.HubContext = app.Services.GetRequiredService<IHubContext<QueueMonitorServiceHub>>()");

                return _context;
            }
            set => _context = value;
        }
        private static IHubContext<QueueMonitorServiceHub> _context = null;

        public static async Task StatusMessage(string message)
        {
            await HubContext.Clients.All.SendAsync("statusMessage", message);
        }

        /// <summary>
        /// Writes out message to all connected SignalR clients
        /// </summary>
        /// <param name="message"></param>
        public static async Task WriteMessage(string message, string id = null, string icon = "Info",
            DateTime? time = null)
        {
            if (id == null)
                id = string.Empty;

            // if no id is passed write the message in the ID area
            // and show no message
            if (string.IsNullOrEmpty(id))
            {
                id = message;
                message = string.Empty;
            }

            if (time == null)
                time = DateTime.UtcNow;

            // Write out message to SignalR clients  
            await HubContext.Clients.All.SendAsync("writeMessage", message,
                icon,
                time.Value.ToString("HH:mm:ss"),
                id,
                string.Empty);
        }


        /// <summary>
        /// Writes out a message to all SignalR clients
        /// </summary>
        /// <param name="queueItem"></param>
        /// <param name="elapsed"></param>
        /// <param name="waiting"></param>
        public static async Task WriteMessage(QueueMessageItem queueItem,
            int elapsed = 0,
            int waiting = -1,
            DateTime? time = null)
        {
            string elapsedString = string.Empty;
            if (elapsed > 0)
                elapsedString = (Convert.ToDecimal(elapsed) / 1000).ToString("N2") + "s";

            var msg = HtmlUtils.DisplayMemo(queueItem.Message);

            if (time == null)
                time = DateTime.UtcNow;

            // Write out message to SignalR clients            
            await HubContext.Clients.All.SendAsync("writeMessage", msg,
                queueItem.Status,
                time.Value.ToString("HH:mm:ss"),
                queueItem.Id,
                elapsedString,
                waiting, queueItem.QueueName);
        }

        /// <summary>
        /// Throws an exception from server to client
        /// On client handle with 
        /// self.hub.server.updateServiceStatus(status).fail(function(err){});
        /// </summary>
        /// <param name="message"></param>
        public async Task ThrowException(string message)
        {
            await StatusMessage(message);
            throw new ApplicationException(message);
        }
    }

    public class QueueControllerStatus
    {
        public string queueName { get; set; }
        public int waitInterval { get; set; }
        public int threadCount { get; set; }
        public bool paused { get; set; }
    }
}