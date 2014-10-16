using Microsoft.Owin;
using Owin;
using QueueManagerStarter;

[assembly: OwinStartup(typeof(oWinStartup))]

namespace QueueManagerStarter
{    
    public class oWinStartup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();

            //"/signalr", map =>
            //{
            //    map.UseCors(CorsOptions.AllowAll);

            //    var hubConfiguration = new HubConfiguration
            //    {
            //        EnableDetailedErrors = true                    
            //    };

            //    map.Map("/signalr",hubConfiguration);
            //});

        }
    }

}