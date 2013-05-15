using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Owin;
using Owin.Types;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Westwind.MessageQueueing.Service
{
    public class SignalRStartup
    {
        public static IAppBuilder App = null;

        public void Configuration(IAppBuilder app)
        {
            HubConfiguration hubConfiguration = new HubConfiguration { 
                EnableCrossDomain = true,
                EnableDetailedErrors = true
            };
            //GlobalHost.HubPipeline.RequireAuthentication();
            //GlobalHost.HubPipeline.AddModule();
                                        
            app.MapHubs(hubConfiguration);                        
        }
        
    }

    public class QueueAuthorizeAttribute : AuthorizeAttribute
    {        
        /// <summary>
        /// Allow only connections if a valid user security token has been passed from
        /// the Web application
        /// </summary>
        /// <param name="hubDescriptor"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public override bool AuthorizeHubConnection(HubDescriptor hubDescriptor, IRequest request)
        {            
            string token = request.QueryString["token"];

            //var tokenBus = new BusSecurityToken();
            //int userId = tokenBus.GetUserIdFromSecurityToken(token);

            //if (userId == -1)
            //    return false;

            //var userBus = new BusUser();
            //if (!userBus.IsUserInRole("Programmer_Role"))            
            //    ThrowException("You're not in the correct role to access this service.");

            // allowed access
            return true;
        }

        public override bool AuthorizeHubMethodInvocation(IHubIncomingInvokerContext hubIncomingInvokerContext, bool appliesToMethod)
        {
            return true;
        }
    }

    //public class AuthenticationPipelineModule : HubPipelineModule
    //{      
    //}
}
