using System;
using System.Web.Hosting;
using Westwind.MessageQueueing.Hosting.ControllerHosting;


namespace Westwind.MessageQueueing.WebHostSample
{
    public class Global : System.Web.HttpApplication
    {
        //private static ApplicationScheduler scheduler;
        private static ServiceLauncher<WebHostSampleQueueController> launcher;

        protected void Application_Start(object sender, EventArgs e)
        {            
            // Pings the service and ensures it stays alive
            //scheduler = new ApplicationScheduler()
            //{
            //    CheckFrequency = 600000
            //};
            //scheduler.Start();            

            // QueueMessageManager Configuration
            launcher = new ServiceLauncher<WebHostSampleQueueController>();

            // customize how the QueueMessageManager is loaded on each request
            launcher.OnCreateQueueManager = () =>
            {
                var manager = new QueueMessageManagerSql();   
             
                //var manager = new QueueMessageManagerSqlMsMq();
                //manager.MsMqQueuePath = @".\private$\";

                return manager;
            };

            launcher.Start();

            // register so shutdown is controlled
            HostingEnvironment.RegisterObject(launcher);
        }

        public void Application_Error()
        {
            //var origEx = Server.GetLastError();
            //var ex = origEx.GetBaseException();

            //string sqlErrors = null;

            //int resultCode = 200;
            //if (ex is HttpException)
            //{
            //    var httpException = (HttpException)ex;
            //    resultCode = httpException.GetHttpCode();
            //}
            //if (ex is SqlException)
            //{
            //    var sqlException = ex as SqlException;
            //    StringBuilder sb = new StringBuilder();
            //    if (sqlException.Errors != null && sqlException.Errors.Count > 0)
            //    {
            //        foreach (SqlError err in sqlException.Errors)
            //        {
            //            sb.AppendLine(err.Procedure + ", " + err.Message);
            //        }
            //    }
            //    sqlErrors = sb.ToString();
            //}

            //var errorHandler = new WebErrorHandler(ex);

            //// Log the error if specified
            //if (LogManagerConfiguration.Current.LogErrors &&
            //    resultCode != 404 && resultCode != 401)
            //{
            //    errorHandler.Parse();
            //    WebLogEntry entry = new WebLogEntry(ex, System.Web.HttpContext.Current);
            //    entry.Details = errorHandler.ToString();
            //    if (!string.IsNullOrEmpty(sqlErrors))
            //        entry.Details += "\r\n" + sqlErrors;

            //    LogManager.Current.WriteEntry(entry);
            //}

            ////            StringBuilder sb = new StringBuilder();
            ////            sb.Append(@"
            ////<p>An unexpected error has occurred in the application. We've logged the error and notified the 
            ////administrator to this problem. 
            ////</p>
            ////
            ////<p>
            ////We're returning you back to the home page of the site.
            ////</p>
            ////
            ////<p>
            ////We apologize for the inconvenience.
            ////</p>
            ////");
            //HttpResponse Response = System.Web.HttpContext.Current.Response;
            //if (Response.Filter != null)
            //{
            //    var f = Response.Filter;
            //    f = null;
            //    Response.Filter = null;
            //}

            //var model = new ErrorViewModel();

            //if (App.AdminConfiguration.DebugMode == DebugModes.Default)
            //{
            //    Response.TrySkipIisCustomErrors = true;
            //    return;
            //}
            //else if (App.AdminConfiguration.DebugMode == DebugModes.DeveloperErrorMessage)
            //{
            //    model.Message = "<pre style='font-size: 10pt'>" + errorHandler.ToString() + "</pre>";
            //    model.MessageIsHtml = true;
            //}
            //else if (App.AdminConfiguration.DebugMode == DebugModes.ApplicationErrorMessage)
            //{
            //    // do nothing - already got our message
            //}

            //Response.ClearContent();
            //Response.ClearHeaders();
            //Server.ClearError();

            //// note: this has to happen here after clearing!
            //Response.TrySkipIisCustomErrors = true;

            ////Response.AddHeader("refresh", "4;url=" + new UrlHelper(Context.Request.RequestContext).Action("index", "home"));

            //ActionResult result;

            //var wrapper = new HttpContextWrapper(HttpContext.Current);
            //var routeData = new RouteData();
            //routeData.Values.Add("controller", "ErrorController");

            //ErrorController controller = new ErrorController();

            //controller.ControllerContext = new ControllerContext(wrapper, routeData, controller);
            //result = controller.ShowErrorViewPage("~/Views/GenericError.cshtml", model);

            //Response.ContentType = "text/html";
            //Response.StatusCode = 500;
            //try
            //{
            //    result.ExecuteResult(controller.ControllerContext);
            //}
            //catch (Exception ex2)
            //{
            //    Response.Write("Unable to process Error Page.</hr>");

            //    if (App.AdminConfiguration.DebugMode == DebugModes.DeveloperErrorMessage)
            //    {
            //        Response.Write("<b>Render Error: " + ex2.Message + "</b></hr>");

            //        Response.Write("<b>Original Error:</b>");
            //        Response.Write("<pre>");
            //        Response.Write(model.Message);
            //        Response.Write("</pre>");
            //    }
            //}
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