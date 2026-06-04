using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueMessageManager.CoreWebSample.Models;

namespace Westwind.QueueMessageManager.CoreWebSample;

public class HomeController : Controller
{
    public class TestWriteMessageRequest
    {
        public string? Message { get; set; }
        public string? Id { get; set; }
        public string? Icon { get; set; }
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

  
}

