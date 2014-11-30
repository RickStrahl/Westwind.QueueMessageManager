using System;
using System.Web.Hosting;
using Westwind.MessageQueueing.Hosting;
namespace Westwind.MessageQueueing.WebHostSample
{
    public class Global : System.Web.HttpApplication
    {
        //private static ApplicationScheduler scheduler;
        private static AspNetServiceLauncher<WebHostSampleQueueController> launcher;

        protected void Application_Start(object sender, EventArgs e)
        {            
            // Pings the service and ensures it stays alive
            //scheduler = new ApplicationScheduler()
            //{
            //    CheckFrequency = 600000
            //};
            //scheduler.Start();            

            // QueueMessageManager Configuration
            launcher = new AspNetServiceLauncher<WebHostSampleQueueController>();

            // customize how the QueueMessageManager is loaded on each request
            launcher.OnCreateQueueManager = () =>
            {
                var manager = new QueueMessageManagerSql();
                //manager.ConnectionString = "server=.;database=MessageQueues;integrated security=true;";
             
                //var manager = new QueueMessageManagerSqlMsMq();
                //manager.MsMqQueuePath = @".\private$\";

                return manager;
            };

            launcher.Start();
        }

        public void Application_Error()
        {
           
        }


        protected void Session_Start(object sender, EventArgs e)
        {

        }


        protected void Application_BeginRequest(object sender, EventArgs e)
        {

        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {

        }

        protected void Session_End(object sender, EventArgs e)
        {

        }


        protected void Application_End()
        {
            try
            {
                //AppUtils.PingQueueServer();
                //Trace.WriteLine("Application Shut Down Ping completed: " + App.AdminConfiguration.MonitorHostUrl + "ping.aspx");
            }
            catch { }            
        }

    }
}