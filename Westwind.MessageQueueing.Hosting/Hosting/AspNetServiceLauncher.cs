using System;
using System.Text;
using System.Threading;
using System.Web.Hosting;
using Westwind.Utilities.Logging;

namespace Westwind.MessageQueueing.Hosting
{
    /// <summary>
    /// This class is the ASP.NET service bootstrapper that loads up
    /// a QueueController and gets it processing queue items.
    /// 
    /// Provive a Multi-Controller type and override the ManagerType
    /// or the OnCreateManager predicate to customize the controller.
    /// 
    /// This class is instantiated at the launch of the server -
    /// in Application_Start or in the oWin bootstrap process 
    /// when self hosting.
    /// </summary>
    public class AspNetServiceLauncher<TController> : ServiceLauncher<TController>, IRegisteredObject
        where TController:  QueueControllerMultiple, new()        
    {
        public AspNetServiceLauncher()
        {
            // Let ASP.NET know this is a background task that
            // needs to be monitored on shutdown
            HostingEnvironment.RegisterObject(this);
        }

        public void Stop(bool immediate = false)
        {
            base.Stop(immediate);

            // Let ASP.NET know it's safe to unload this object
            HostingEnvironment.UnregisterObject(this); 
        }
    }
}