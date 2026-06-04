using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using System.Runtime.InteropServices;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.CoreWebSample;
using Westwind.Utilities;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;


qmmApp.IsDevelopment = builder.Environment.IsDevelopment();
qmmApp.EnvironmentName = builder.Environment.EnvironmentName;
qmmApp.Constants.StartupFolder = Environment.CurrentDirectory;
qmmApp.Constants.WebRootFolder = Path.Combine(qmmApp.Constants.StartupFolder, "wwwroot");

var configFile = "_qmmApp-configuration.json";
var noConfig = !File.Exists(configFile);

var appConfig = qmmApp.Configuration;
builder.Configuration.GetSection("qmmApp").Bind(appConfig);
services.AddSingleton(appConfig);

if (noConfig)
{
    appConfig.Write();
    Console.WriteLine($"Configuration file '{configFile}' was created. Review it, and set default values, and restart the application.");
    return;
}



// Add services to the container.
builder.Services.AddControllersWithViews();

// Authorization builder related
builder.Services.AddSingleton<
    IAuthorizationHandler,
    QueueMonitorAccessHandler>();

builder.Services.AddAuthorization(options =>
{
    // Register [QueueMonitorAuthorize] attribute
    options.AddPolicy("QueueMonitorAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new QueueMonitorAccessRequirement());
    });
});

builder.Services.AddSignalR();


// create a new Controller to process in the background
// on separate threads
QmmGlobals.Controller  = new QueueControllerMultiple(controllers: new List<QueueController>()
{
    new QueueControllerMultiple()
    {
        QueueName = "Queue1",
        WaitInterval = 300,
        ThreadCount = 5
    },
    new QueueControllerMultiple()
    {
        QueueName = "Queue2",
        WaitInterval = 500,
        ThreadCount = 3
    }
});

//builder.Services.AddQueueHubAuthorization();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(qmmApp.Constants.WebRootFolder)
});
app.MapStaticAssets();
app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.MapHub<QueueMonitorServiceHub>("/queueMonitorServiceHub");
QueueMonitorServiceHub.HubContext = app.Services.GetRequiredService<IHubContext<QueueMonitorServiceHub>>();

app.Start();

Console.ForegroundColor = ConsoleColor.DarkYellow;
Console.WriteLine($@"---------------------------------
QMM Sample App v{qmmApp.Version}
---------------------------------");
Console.ResetColor();

var urlList = app.Urls;
string urls = string.Join(" ", urlList);

Console.Write("    Urls: ");
Console.ForegroundColor = ConsoleColor.DarkCyan;
Console.WriteLine(urls, ConsoleColor.DarkCyan);
Console.ResetColor();

Console.WriteLine($" Runtime: {RuntimeInformation.FrameworkDescription} - {builder.Environment.EnvironmentName}");
Console.WriteLine($"Platform: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
Console.WriteLine("Connection: " + PasswordScrubber.Instance.ScrubSqlConnectionStringValues(qmmApp.Configuration.ConnectionString));
Console.WriteLine();

//Console.WriteLine(config.ConnectionString);

app.WaitForShutdown();
