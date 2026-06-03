using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class QueueMonitorAuthorizeAttribute : AuthorizeAttribute
{
    public QueueMonitorAuthorizeAttribute()
    {
        Policy = "QueueMonitorAccess";
    }
}

public class QueueMonitorAccessRequirement : IAuthorizationRequirement
{
}


public class QueueMonitorAccessHandler :
    AuthorizationHandler<QueueMonitorAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        QueueMonitorAccessRequirement requirement)
    {
        var user = context.User;

        if (user?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var tokenType = user.FindFirst("token_type")?.Value;
        var queueAccess = user.FindFirst("queue_access")?.Value;

        if (tokenType == "service" && queueAccess == "monitor")
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

// using Microsoft.AspNetCore.Authorization;

// namespace QueueManagerStarter
// {
//     /// <summary>
//     /// Custom Authentication that is checked when the service 
//     /// is accessed. This scheme looks for a token and validates
//     /// that the valid token exists.
//     /// </summary>
//     public class QueueAuthorizeAttribute : AuthorizeAttribute
//     {        
//         /// <summary>
//         /// Allow only connections if a valid user security token has been passed from
//         /// the Web application
//         /// </summary>
//         /// <param name="hubDescriptor"></param>
//         /// <param name="request"></param>
//         /// <returns></returns>
//         public override bool AuthorizeHubConnection(HubDescriptor hubDescriptor, IRequest request)
//         {            
//             string token = request.QueryString["token"];

//             //var tokenBus = new BusSecurityToken();
//             //int userId = tokenBus.GetUserIdFromSecurityToken(token,true);
            
//             //if (userId == -1)
//             //    return false;

//             //var userBus = new BusUser();
//             //if (!userBus.IsUserInRole("Programmer_Role"))            
//             //    ThrowException("You're not in the correct role to access this service.");

//             // allowed access
//             return true;
//         }

//         public override bool AuthorizeHubMethodInvocation(IHubIncomingInvokerContext hubIncomingInvokerContext, bool appliesToMethod)
//         {
//             return true;
//         }
//     }
// }
