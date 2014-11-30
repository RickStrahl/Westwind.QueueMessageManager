using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Owin;
using QueueManagerStarter;

[assembly: OwinStartup(typeof(oWinStartup))]

namespace QueueManagerStarter
{    
    public class oWinStartup
    {
        public void Configuration(IAppBuilder app)
        {
            // Default local Site only configuration
            //app.MapSignalR();

            app.Map("/signalr", map =>
            {
                // Add Cors Behavior
                map.UseCors(CorsOptions.AllowAll);

                var hubConfiguration = new HubConfiguration
                {
                    EnableDetailedErrors = true
                };
                map.RunSignalR(hubConfiguration);
            });

        }
    }

}