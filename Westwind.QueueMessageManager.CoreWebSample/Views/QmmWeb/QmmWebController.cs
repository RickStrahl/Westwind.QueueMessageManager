using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using Westwind.AspNetCore;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.CoreWebSample;
using Westwind.QueueMessageManager.CoreWebSample.Models;

namespace Westwind.QueueMessageManager.CoreWebSample;

[Route("/qmm")]
public class QmmWebController : BaseApiController
{

    [Route("/qmm/queuemonitor")]
    public IActionResult QueueMonitor()
    {
        return View("~/views/qmmweb/queuemonitor.cshtml");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
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
        
        item.Id = "qmmweb-" + qmmApp.NewId();
        manager.SubmitRequest(item, autoSave: true);
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