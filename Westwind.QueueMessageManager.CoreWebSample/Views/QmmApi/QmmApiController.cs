using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using Westwind.AspNetCore;
using Westwind.AspNetCore.Errors;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.CoreWebSample;
using Westwind.QueueMessageManager.CoreWebSample.Models;

namespace Westwind.QueueMessageManager.CoreWebSample;

[Route("/qmm")]
public class QmmApiController : BaseApiController
{

    [Route("/qmm/queuemonitor")]
    public IActionResult QueueMonitor()
    {
        return View("~/views/qmmapi/queuemonitor.cshtml");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet("/api/qmm/get-message/{id}")]
    public async Task<IActionResult> GetMessage([FromRoute] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ApiException("Message Id is required.",404);

        using var manager = new QueueMessageManagerSql(qmmApp.ConnectionString);
        QueueMessageItem item;
        if (id != "-1")
            item = manager.Load(id);
        else
            item = manager.GetCompleteQueueMessages("Test1", 1).FirstOrDefault();
        
        if (item == null)
            throw new ApiException("Message not found.",404);

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
        if(!manager.SubmitRequest(item, autoSave: true))        
        {
            throw new ApiException("Failed to submit item to queue: " + manager.ErrorMessage);
        }

        await QueueMonitorServiceHub.WriteMessageInternal(item);
        await QueueMonitorServiceHub.GetWaitingQueueMessageCountInternal(item.QueueName);

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
        await QueueMonitorServiceHub.GetWaitingQueueMessageCountInternal(item.QueueName);

        return Ok(new { success = true, sentAt = DateTime.UtcNow });
    }
}

public class TestWriteMessageRequest
{
    public QueueMessageItem Item { get; set; }
    public string? Icon { get; set; }
}