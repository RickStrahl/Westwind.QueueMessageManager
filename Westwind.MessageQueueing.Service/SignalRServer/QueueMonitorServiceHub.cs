using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Service;

namespace Westwind.MessageQueueing.Service
{
    [QueueAuthorize]
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
            GlobalService.Controller.Paused = false;

            Clients.All.startServiceCallback(true);
            Clients.All.writeMessage("Queue starting with " + GlobalService.Controller.ThreadCount.ToString() + " threads.",
                                     "Info", DateTime.Now.ToString("HH:mm:ss"));                        
        }

        public void StopService()
        {
            // Pause - we can't stop because that'll exit the server            
            GlobalService.Controller.Paused = true;
            Clients.All.stopServiceCallback(true);

            Clients.All.writeMessage("Queue has been stopped.","Info",DateTime.Now.ToString("HH:mm:ss"));
        }

        

        public void GetServiceStatus()
        {
            var instance = GlobalService.Controller;
            if (instance == null)
                Clients.Caller.getServiceStatusCallback(null);

            Clients.Caller.getServiceStatusCallback(
                new QueueControllerStatus()
                {
                    queueName = instance.QueueName,
                    waitInterval = instance.WaitInterval,
                    threadCount = instance.ThreadCount,   
                    paused = instance.Paused
                });
        }


        public void UpdateServiceStatus(QueueControllerStatus status)
        {                     

            if (status == null)
            {
                Clients.Caller.updateServiceStatus(null);
                return;
            }

            var controller = GlobalService.Controller;
            controller.WaitInterval = status.waitInterval;
            controller.QueueName = status.queueName;
            controller.ThreadCount = status.threadCount;            
            
            if (controller.ThreadCount > 20)
            {
                controller.ThreadCount = 20;
                status.threadCount = 20;
            }

            var config = QueueMessageManagerConfiguration.Current;
            config.ControllerThreads = controller.ThreadCount;
            config.WaitInterval = controller.WaitInterval;
            config.QueueName = status.queueName;
            
            // try to save config settings
            config.Write();            

            controller.StopProcessing();
            //Thread.Sleep(1000);            
            controller.StartProcessingAsync();
            
            // update all clients with the status information
            Clients.All.updateServiceStatusCallback(status);

            StatusMessage("Service Status settings updated.",true);
        }

        public void GetWaitingQueueMessageCount()
        {
            using (var manager = new QueueMessageManager())
            {
                int count = manager.GetWaitingQueueMessageCount(GlobalService.Controller.QueueName);
                // broadcast to all clients
                Clients.All.getWaitingQueueMessageCountCallback(count);
            }
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
        static IHubContext _context = null;


        /// <summary>
        /// Writes out message to all connected SignalR clients
        /// </summary>
        /// <param name="message"></param>
        public static void WriteMessage(string message)
        {

            // Write out message to SignalR clients  
            HubContext.Clients.All.writeMessage(string.Empty,
                "Info",
                DateTime.Now.ToString("HH:mm:ss"),
                message,  // write message as ID in UI
                string.Empty);
        }
        

        /// <summary>
        /// Writes out a message to all SignalR clients
        /// </summary>
        /// <param name="queueItem"></param>
        /// <param name="elapsed"></param>
        /// <param name="waiting"></param>
        public static void WriteMessage(QueueMessageItem queueItem, int elapsed = 0, int waiting = -1)
        {
            string elapsedString = string.Empty;
            if (elapsed > 0)
                elapsedString = (Convert.ToDecimal(elapsed) / 1000).ToString("N2") + "s";

            // Write out message to SignalR clients            
             HubContext.Clients.All.writeMessage(queueItem.Message,
                    queueItem.Status,
                    DateTime.Now.ToString("HH:mm:ss"),
                    queueItem.Id,
                    elapsedString,
                    waiting);
        }

        /// <summary>
        /// Throws an exception from server to client
        /// On client handle with 
        /// self.hub.server.updateServiceStatus(status).fail(function(err){});
        /// </summary>
        /// <param name="message"></param>
        public void ThrowException(string message)
        {
            this.StatusMessage(message);
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
