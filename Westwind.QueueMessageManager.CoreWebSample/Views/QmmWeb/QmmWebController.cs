using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueMessageManager.CoreWebSample.Models;

namespace Westwind.QueueMessageManager.CoreWebSample;

[Route("/qmm")]
public class QmmWebController : Controller
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

        await QueueMonitorServiceHub.WriteMessage(item);

        return Ok(new { success = true, sentAt = DateTime.UtcNow });
    } 
}

public class TestWriteMessageRequest
{
    public QueueMessageItem Item { get; set; }
    public string? Icon { get; set; }
}