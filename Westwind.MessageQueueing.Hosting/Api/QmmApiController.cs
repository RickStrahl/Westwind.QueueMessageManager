using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Westwind.AspNetCore;
using Westwind.AspNetCore.Errors;
using Westwind.AspNetCore.Extensions;
using Westwind.Utilities;
using Westwind.Utilities.Data.Security;

namespace Westwind.MessageQueueing.Hosting;


/// <summary>
/// Base API Controller for the QMM API. Implement a subclass in the
/// main Web application to inherit all of this functionality and
/// optionally override additional behavior. 
/// 
/// Note the class has to be implememnted in order for routes to be applied.
/// </summary>
public abstract class QmmApiController : BaseApiController
{
    protected bool RequiresAuthentication { get; set; } = false;


    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        // checks for authentication - throws API exception on failure
        if (RequiresAuthentication)
        {
            string action = context.ActionDescriptor.RouteValues["action"];
            if (string.IsNullOrEmpty(action))            
                action = context.HttpContext.Request.Query["action"].FirstOrDefault() ?? string.Empty;
            
            if (StringUtils.Inlist(action, StringComparison.OrdinalIgnoreCase, ["error", "ping", "queuemonitor", "authenticate"]))
                return;

            var token = Request.Headers.Authorization.FirstOrDefault();
            if (token == null)
                throw new ApiException("Invalid or missing Authorization token.", 401);

            if (!OnValidateToken(token))
                throw new ApiException("Invalid or expired Authorization token. Please sign in with Authenticate().", 401);
        }        
    }


    /// <summary>
    /// Method that can be used to override User Authentication. Default implementation
    /// checks for empty username and password only - application should validate users
    /// based on application's authentication logic from business objects or identity.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    protected virtual bool OnAuthenticateUser(UserViewModel user)
    {
        if (string.IsNullOrEmpty(user.UserName) || string.IsNullOrEmpty(user.Password))
            return false;

        return true;
    }



    /// <summary>
    /// Token creation method that creates a token object. Default
    /// creates a token based on the User's username.
    /// 
    /// Returns Token and Expiration.
    /// </summary>
    /// <param name="user">Passed User record</param>
    /// <returns></returns>
    protected virtual TokenResult OnCreateToken(UserViewModel user)
    {
        var tokenManager = new UserTokenManager(qmmApp.ConnectionString);
        string token = tokenManager.CreateNewToken(user.UserName);
        return new TokenResult { Token = token, TokenExpiration = DateTime.UtcNow.AddSeconds(tokenManager.TokenTimeoutSeconds) };
    }

    /// <summary>
    /// Method that handles token validation using the UserTokenManager by default.
    /// You can override this method to implement your own token validation logic.       
    /// </summary>
    /// <param name="context">Action context that gives access to headers</param>
    protected virtual bool OnValidateToken(string token)
    {
        var tokenManager = new UserTokenManager(qmmApp.ConnectionString);
        return tokenManager.IsTokenValid(token, renewLease: true);
    }



    #region Html Page Requests

    [Route("/qmm/queuemonitor")]
    public IActionResult QueueMonitor()
    {
        if (qmmApp.Configuration.DisableQueueMonitor)
        {
            return NotFound("Queue Monitor is disabled.");
        }
        return View("~/views/qmmapi/queuemonitor.cshtml");
    }


    [HttpGet]
    [Route("/qmm/configuration")]
    public ActionResult ShowConfiguration()
    {
        if (qmmApp.Configuration.DisableQueueMonitor)
        {
            return NotFound("Queue  Configuration is disabled.");
        }
        var model = CreateViewModel<AdminViewModel>();
        model.ConfigurationJson = JsonSerializationUtils.Serialize(qmmApp.Configuration, false, true, false);
        model.ContainerConfigurationJson = JsonSerializationUtils.Serialize(QueueContainer.Current, false, true, false);

       
       return View("~/views/qmmapi/QmmContainerConfiguration.cshtml", model);
    }



    [HttpPost]
    [Route("/qmm/configuration")]
    public ActionResult UpdateConfiguration(AdminViewModel model, [FromServices] IHostApplicationLifetime appLifetime)
    {
        InitializeViewModel(model);

        if (Request.IsFormVar("btnRestartApplication"))
        {
            // touch web.config - pending permissions
            var webconfig = Path.Combine(qmmApp.Constants.StartupFolder, "web.config");

            try
            {
                appLifetime.StopApplication();
                // var fi = new FileInfo(webconfig);
                // fi.LastWriteTime = DateTime.Now;
                ErrorDisplay.ShowSuccess("IIS Application Pool has been reloaded.");
                Response.AddMetaRefreshTagHeader("/admin", 2);
            }
            catch (Exception ex)
            {
                ErrorDisplay.ShowError(ex.Message, "IIS App reloading failed or not running on IIS.");
            }
        }
        else if(Request.IsFormVar("btnWriteContainerConfiguration"))
        {
            // update from current configuration that was just entered
            var containerConfig = QueueContainer.CreateFromConfigurationString(model.ContainerConfigurationJson);
                //JsonSerializationUtils.Deserialize(model.ContainerConfigurationJson, typeof(QueueContainer)) as QueueContainer;

            if (containerConfig != null)
            {
                QueueContainer.Current.StopProcessing();                
                QueueContainer.Current.Dispose();
                QueueContainer.Current = null;                

                QueueContainer.Current = containerConfig;                               
                QueueContainer.Current.StartProcessingAsync();
               
                // write it back out
                JsonSerializationUtils.SerializeToFile(containerConfig, Path.Combine(qmmApp.Constants.StartupFolder, "_qmm-container-config.json"), false, true);

                model.ErrorDisplay.ShowInfo("Container configuration has been updated.");
            }
            else
            {
                model.ErrorDisplay.ShowError("Container Configuration could not be updated - invalid JSON.");
            }

            // see actual current values
            //ModelState.Clear();
            model.ContainerConfigurationJson = JsonSerializationUtils.Serialize(QueueContainer.Current, false, true, false);
        }
        else if (Request.IsFormVar("btnWriteConfiguration"))
        {
            var config =
                JsonSerializationUtils.Deserialize(model.ConfigurationJson, typeof(qmmAppConfiguration)) as qmmAppConfiguration;

            if (config != null)
            {
                qmmApp.Configuration = config;
                qmmApp.Configuration.Write();

                model.ErrorDisplay.ShowInfo("Container configuration has been updated.");
            }
            else
            {
                model.ErrorDisplay.ShowError("Container Configuration could not be updated - invalid JSON.");
            }
        }

        

        return View("~/views/qmmapi/QmmContainerConfiguration.cshtml", model);
    }



    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    #endregion



    /// <summary>
    /// API Authentication endpoint that returns a token for a valid user.
    /// Base implementation just checks for any username and password to accept
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    [HttpPost("/api/qmm/authenticate")]
    public virtual async Task<IActionResult> Authenticate([FromBody] UserViewModel user)
    {
        if (RequiresAuthentication)
        {
            if (user == null)
                throw new ApiException("UserName and Password are required.", 401);
            
            if(!OnAuthenticateUser(user))
            {
                throw new ApiException("Invalid username or password.");
            }            
        }

        var token = OnCreateToken(user);
        return Ok(token);
    }



    [HttpGet("/api/qmm/get-message/{id}")]
    public async Task<IActionResult> GetMessage([FromRoute] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ApiException("Message Id is required.", 404);

        using var manager = new QueueMessageManagerSql(qmmApp.ConnectionString);
        QueueMessageItem item;
        if (id != "-1")
            item = manager.Load(id);
        else
            item = manager.GetCompleteQueueMessages("Test1", 1).FirstOrDefault();

        if (item == null)
            throw new ApiException("Message not found.", 404);

        return Json(item);
    }

    [HttpPost("/api/qmm/write-message")]
    public async Task<IActionResult> WriteTestMessage([FromBody] QueueMessageItem item)
    {
        if (string.IsNullOrWhiteSpace(item?.Message))
            return BadRequest(new { error = "Message is required." });

        await QueueMonitorServiceHub.WriteMessageInternal(item);

        return Ok(new { success = true, sentAt = DateTime.UtcNow });
    }

    [HttpPost("/api/qmm/submit-item")]
    public async Task<IActionResult> SubmitItem([FromBody] QueueMessageItem item)
    {
        if (string.IsNullOrWhiteSpace(item?.Message))
            return BadRequest(new { error = "Message is required." });

        using var manager = new QueueMessageManagerSql(qmmApp.ConnectionString);

        item.Id = qmmApp.NewId();
        if (!manager.SubmitRequest(item, autoSave: true))
        {
            throw new ApiException("Failed to submit item to queue: " + manager.ErrorMessage);
        }

        await QueueMonitorServiceHub.WriteMessageInternal(item);
        
        return Ok(new { success = true, sentAt = DateTime.UtcNow });
    }

    [HttpPost("/api/qmm/update-item")]
    public async Task<IActionResult> UpdateItem([FromBody] QueueMessageItem item)
    {
        if (string.IsNullOrWhiteSpace(item?.Id))
            return NotFound(new { error = "Existing Message Id is required." });


        using var manager = new QueueMessageManagerSql(qmmApp.ConnectionString);

        // Load the existing item and update it with some data from the incoming item.
        var loadedItem = manager.Load(item.Id);
        if (loadedItem == null)
        {
            return NotFound(new { error = "Existing Message Id is required." });
        }

        manager.UpdateQueueMessageStatus(loadedItem, item.Status, item.Message);

        await QueueMonitorServiceHub.WriteMessageInternal(loadedItem);
        
        return Ok(new { success = true, sentAt = DateTime.UtcNow });
    }


}


public class UserViewModel
{
    /// <summary>
    /// Username if used for authentication
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Password if used
    /// </summary>
    public string Password { get; set; }


    /// <summary>
    /// Optional API Key
    /// 
    /// not used by default implementation
    /// </summary>
    public string Apikey { get; set; }

    /// <summary>
    /// Optional Id for the message
    /// 
    /// Not used by default implementation
    /// </summary>
    public string Id { get; set; }  
   

    /// <summary>
    /// Optional additional data that can be passed to the API
    /// for authentication or token creation
    /// 
    /// Not used by default implementation
    /// </summary>
    public string AdditionalData { get; set; }
}



public class TokenResult
{ 
    /// <summary>
    /// String token generated
    /// </summary>
    public string Token { get; set; }


    /// <summary>
    /// Token Expiration in UTC time
    /// </summary>
    public DateTime TokenExpiration { get; set; }

}


public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public class AdminViewModel : BaseViewModel
{
    public string Message { get; set; }

    public string ApplicationVersion { get; } = typeof(qmmApp).Assembly.GetName().Version.ToString();


    public string ApplicationDate { get; } =
        TimeUtils.FriendlyDateString(new FileInfo(typeof(qmmApp).Assembly.Location).LastWriteTime);

    public string RuntimeVersion { get; } = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    public string ConfigurationJson { get; set; }

    public string ContainerConfigurationJson { get; set; }
}
