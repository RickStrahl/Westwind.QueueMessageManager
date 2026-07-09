using Microsoft.AspNetCore.Mvc;
using Westwind.AspNetCore;
using Westwind.AspNetCore.Errors;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.Hosting;

namespace Westwind.QueueMessageManager.CoreWebSample;

public class SampleAppQmmApiController : QmmApiController
{

    public SampleAppQmmApiController()
    {
        RequiresAuthentication = false;
    }

    ///// <summary>
    ///// API Authentication endpoint that returns a token for a valid user.
    ///// Base implementation just checks for a any username and password to accept
    ///// </summary>
    ///// <param name="user"></param>
    ///// <returns></returns>
    //[HttpPost("/api/qmm/authenticate")]
    //public override async Task<IActionResult> Authenticate([FromBody] UserViewModel user)
    //{
    //    if (user == null || string.IsNullOrEmpty(user.UserName) || string.IsNullOrEmpty(user.Password))
    //        throw new ApiException("UserName and Password are required.", 401);

    //    var tokenManager = new UserTokenManager(qmmApp.ConnectionString);
    //    var token = tokenManager.CreateNewToken(user.UserName);


    //    return Ok(new { token = token, expiresIn = tokenManager.TokenTimeoutSeconds, overridden = true });
    //}

    [HttpGet("/api/qmm/get-message2/{id}")]
    public async Task<IActionResult> GetMessage2([FromRoute] string id)
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


}
