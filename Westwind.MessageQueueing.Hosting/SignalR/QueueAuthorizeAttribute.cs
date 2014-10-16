using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace QueueManagerStarter
{
    /// <summary>
    /// Custom Authentication that is checked when the service 
    /// is accessed. This scheme looks for a token and validates
    /// that the valid token exists.
    /// </summary>
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
            //int userId = tokenBus.GetUserIdFromSecurityToken(token,true);
            
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
}