using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Westwind.MessageQueueing;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Hosting
{
    //[QueueAuthorize]
    public class QueueMonitorServiceHub : Hub
    {
        /// <summary>
        /// Writes a message to the client that displays on the status bar
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allClients"></param>
        public void StatusMessage(string message, bool allClients = false)
        {
            if (allClients)
                Clients.All.statusMessage(message);
            else
                Clients.Caller.statusMessage(message);
        }

        /// <summary>
        /// Starts the service
        /// </summary>
        public void StartService()
        {
            // unpause the QueueController to start processing again
            Globals.Controller.PauseProcessing(false);            

            Clients.All.startServiceCallback(true);

            Clients.All.writeMessage("Queues starting with " + Globals.Controller.ThreadCount.ToString() + " threads.",
                "Info", DateTime.Now.ToString("HH:mm:ss"));
        }

        public void StopService()
        {
            // Pause - we can't stop service because that'll exit the server            
            Globals.Controller.PauseProcessing(true);            

            Clients.All.stopServiceCallback(true);

            Clients.All.writeMessage("Queue has been stopped.", "Info", DateTime.Now.ToString("HH:mm:ss"));
        }


        /// <summary>
        /// Gets the intial set of messages to be displayed by the QueueManager
        /// </summary>
        public void GetInitialMessages(string queueName = null)
        {
            if (string.IsNullOrEmpty(queueName))
                queueName = null;
            
            var queue = new QueueMessageManagerSql();
            var msgs = queue.GetRecentQueueItems(queueName, 10);

            if (msgs == null)
                return;

            foreach (var msg in msgs.Reverse())
            {
                int elapsed = 0;
                DateTime time = DateTime.UtcNow;
                if (msg.Completed != null)
                {
                    if (msg.Started != null)
                        elapsed = (int) (msg.Completed.Value - msg.Started.Value).TotalMilliseconds;
                    time = msg.Completed.Value;
                }
                else if (msg.Started != null)
                {
                    time = msg.Started.Value;
                }

                WriteMessage(msg, elapsed, -1, time);
            }
        }

        public void getQueueNames()
        {            
            var queues = new List<string>();
            foreach (var controller in Globals.Controller.Controllers)
            {
                queues.Add(controller.QueueName);
            }

            Clients.Caller.getQueueNamesCallback(queues);
        }

        public void getQueueMessage(string id)
        {
            var queue = new QueueMessageManagerSql();
            var qitem = queue.Load(id);            
            Clients.Caller.getQueueMessageCallback(qitem);
        }

        public void GetServiceStatus(string queueName)
        {
            var controller = Globals.Controller;
            if (controller.Controllers == null || string.IsNullOrEmpty(queueName))
                controller = null;

            if (controller == null)
            {
                Clients.Caller.getServiceStatusCallback(null);
                return;
            }

            var inst = controller.Controllers
                .FirstOrDefault(ctl => ctl.QueueName == queueName);
                               

            if (inst == null)
                Clients.Caller.getServiceStatusCallback(null);

            Clients.Caller.getServiceStatusCallback(
                new QueueControllerStatus()
                {
                    queueName = inst.QueueName,
                    waitInterval = inst.WaitInterval,
                    threadCount = inst.ThreadCount,
                    paused = inst.Paused
                });
        }


        public void UpdateServiceStatus(QueueControllerStatus status)
        {

            if (status == null)
            {
                Clients.Caller.updateServiceStatus(null);
                return;
            }

            var controller = Globals.Controller.Controllers
                .FirstOrDefault(ct => ct.QueueName == status.queueName);

            if (controller == null)
                return;
            
            controller.WaitInterval = status.waitInterval;
            controller.QueueName = status.queueName;
            controller.ThreadCount = status.threadCount;

            if (controller.ThreadCount > 20)
            {
                controller.ThreadCount = 20;
                status.threadCount = 20;
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
            Clients.All.updateControllerStatusCallback(status);
        }

        public void GetWaitingQueueMessageCount(string queueName = null)
        {
            if (string.IsNullOrEmpty(queueName))
                queueName = null;  // force all

            using (var manager = new QueueMessageManagerSql())
            {
                int count = manager.GetWaitingQueueMessageCount(queueName);
                // broadcast to all clients
                Clients.All.getWaitingQueueMessageCountCallback(count);
            }
        }


        public void Notify(QueueMessageItem queueItem, int elapsed = 0, int waiting = 0)
        {
            WriteMessage(queueItem, elapsed, waiting); 
        }


        /// 
        /// *** Client Broadcast Services
        /// 


        /// <summary>
        /// Context instance to access client connections to broadcast to
        /// </summary>
        public static IHubContext HubContext
        {
            get
            {
                if (_context == null)
                    _context = GlobalHost.ConnectionManager.GetHubContext<QueueMonitorServiceHub>();

                return _context;
            }
        }

        private static IHubContext _context = null;

        public static void StatusMessage(string message)
        {
            HubContext.Clients.All.statusMessage(message);
        }

        /// <summary>
        /// Writes out message to all connected SignalR clients
        /// </summary>
        /// <param name="message"></param>
        public static void WriteMessage(string message, string id = null, string icon = "Info", DateTime? time = null)
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
            HubContext.Clients.All.writeMessage(message,
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
        public static void WriteMessage(QueueMessageItem queueItem, 
            int elapsed = 0, 
            int waiting = -1,
            DateTime? time = null)
        {
            string elapsedString = string.Empty;
            if (elapsed > 0)
                elapsedString = (Convert.ToDecimal(elapsed)/1000).ToString("N2") + "s";

            var msg = HtmlUtils.DisplayMemo(queueItem.Message);

            if (time == null)
                time = DateTime.UtcNow;

            // Write out message to SignalR clients            
            HubContext.Clients.All.writeMessage(msg,
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
        public void ThrowException(string message)
        {
            StatusMessage(message);
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


