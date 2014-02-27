using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using Westwind.MessageQueueing;
using Westwind.Utilities.Logging;
using Microsoft.Owin.Hosting;
using Microsoft.AspNet.SignalR;
using System.Threading;

namespace Westwind.MessageQueueing.Service
{
    /// <summary>
    /// Global refs to the Service and Controller
    /// to keep them alive and running and also to
    /// allow global access from anywhere
    /// </summary>
    public class GlobalService
    {
        public static QueueController<QueueMessageManagerSql> Controller;       
        public static IQueueService Service;        
    }
    
    public interface IQueueService        
    {
        void Start();
        void Stop();
    }

    public class QueueService<T> : ServiceBase, IQueueService, IDisposable
        where T :  QueueController,new()
    {
        public T Controller { get; set; }
        public IDisposable SignalR { get; set; }

        // global instances that keep controller and windows service alive

        public void Start()
        {
            LogManager.Current.LogInfo("Start called");

            var config = QueueMessageManagerConfiguration.Current;
            Controller = new T()
            {
                ConnectionString = config.ConnectionString,
                QueueName = config.QueueName,
                WaitInterval = config.WaitInterval,
                ThreadCount = config.ControllerThreads                
            };
            

            LogManager.Current.LogInfo("Controller created.");
            
            // asynchronously start the SignalR hub
            SignalR = WebApplication.Start<SignalRStartup>("http://*:8080/");

            // *** Spin up n Number of threads to process requests
            Controller.StartProcessingAsync();

            LogManager.Current.LogInfo(String.Format("QueueManager Controller Started with {0} threads.",
                                       Controller.ThreadCount));            

            // Set static instances so that these 'services' stick around
            GlobalService.Controller = Controller;
            GlobalService.Service = this;
        }

        public new void Stop()
        {            
            LogManager.Current.LogInfo("QueueManager Controller Stopped.");              
            Controller.StopProcessing();
            SignalR.Dispose();

            Thread.Sleep(1500);
        }


        /// <summary>
        /// Set things in motion so your service can do its work.
        /// </summary>
        protected override void OnStart(string[] args)
        {
            Start();
        }

        /// <summary>
        /// Stop this service.
        /// </summary>
        protected override void OnStop()
        {
            Stop();
        }

        protected override void OnPause()
        {
            Controller.StopProcessing();              
        }

        protected override void OnContinue()
        {
            Controller.StartProcessingAsync();    
        }
        

        protected override void OnCustomCommand(int command)
        {
            base.OnCustomCommand(command);
        }

        public void Dispose()
        {
            base.Dispose();
        }
        protected override void Dispose(bool disposing)
        {
            if (SignalR != null)
            {
                SignalR.Dispose();
                SignalR = null;
            }
        }
    }
}