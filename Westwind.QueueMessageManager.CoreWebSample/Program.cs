using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.File;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Westwind.AspNetCore.LiveReload;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.CoreWebSample;
using Westwind.QueueMessageManager.CoreWebSample;
using Westwind.Utilities;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;


qmmApp.IsDevelopment = builder.Environment.IsDevelopment();
qmmApp.EnvironmentName = builder.Environment.EnvironmentName;
qmmApp.Constants.StartupFolder = Environment.CurrentDirectory;
qmmApp.Constants.WebRootFolder = Path.Combine(qmmApp.Constants.StartupFolder, "wwwroot");

var configFile = "_qmmApp-configuration.json";
var configExists = File.Exists(configFile);

var appConfig = qmmApp.Configuration;
builder.Configuration.GetSection("qmmApp").Bind(appConfig);
services.AddSingleton(appConfig);

if (!configExists)
{
    appConfig.Write();
    Console.WriteLine($"Configuration file '{configFile}' was created. Review it, and set default values, and restart the application.");
    return;
}


builder.Logging.ClearProviders();

// logging
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    //outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}---{NewLine}")
    .WriteTo.File(
        Path.Combine(qmmApp.Constants.WebRootFolder, "admin", "applicationlog.txt"),
        fileSizeLimitBytes: 3_000_000,
        retainedFileCountLimit: 5,
        rollOnFileSizeLimit: true,
        shared: true,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}---{NewLine}",
        flushToDiskInterval: TimeSpan.FromSeconds(20));

Log.Logger = logConfig.CreateLogger();
Log.Information("Application Started.");
builder.Services.AddSerilog();

if (qmmApp.Configuration.System.LiveReloadEnabled)
{
    services.AddLiveReload(config =>
    {
        config.LiveReloadEnabled = qmmApp.Configuration.System.LiveReloadEnabled;
        config.RefreshInclusionFilter = path =>
        {
            if (path.Contains("/LocalizationAdmin", StringComparison.OrdinalIgnoreCase))
                return RefreshInclusionModes.DontRefresh;

            return RefreshInclusionModes.ContinueProcessing;
        };
    });
}


var mvcBuilder = services.AddControllersWithViews()
    .AddNewtonsoftJson(opt =>
    {
        if (builder.Environment.IsDevelopment())
            opt.SerializerSettings.Formatting = Formatting.Indented;
    });

if (appConfig.System.LiveReloadEnabled)
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

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


var config = QueueMessageManagerConfiguration.Current;


//var launcher = new ServiceLauncher<TestQmmController>()
//{
//    LogManager = Log.Logger as Microsoft.Extensions.Logging.ILogger
//};

// create a new Controller to process in the background
// on separate threads


var controller = new QueueControllerMultiple(
    connectionString: qmmApp.ConnectionString,
    controllers: [
        new Test1Queue(),
        new Test2Queue()
    ]);

//var controller = new TestQmmController(connectionString: qmmApp.ConnectionString);
QmmGlobals.Controller = controller;
controller.StartProcessingAsync();


//var controller = new QueueControllerMultiple(config, qmmApp.ConnectionString);
//controller.ExecuteStart += async manager =>
//{
//    var item = manager.Item;
//    var swatch = Stopwatch.StartNew();

//    // TEST ONLY
//    await Task.Delay(1000); // so we can see submission
//    //Thread.Sleep(1000);

//    try
//    {
//        if (item.Action == "PRINT")
//        {
//            item.Message = "Started on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;
//            manager.StartRequest();
//            manager.Save();
//            QueueMonitorServiceHub.WriteMessageInternal(item).FireAndForget();

//            await Task.Delay(3000);
//            //Thread.Sleep(3000);
//            item.Message = "Completed on: " + DateTime.Now + " - " + item.Message + " - Thread: " + Thread.CurrentThread.ManagedThreadId;

//            manager.CompleteRequest();
//            manager.Save();
//        }
//        else
//        {
//            //Thread.Sleep(1200);
//            await Task.Delay(1200);
//            manager.FailRequest(messageText: "Unknown action: " + item.Action);
//            manager.Save();
//        }
//    }
//    catch (Exception ex)
//    {
//        manager.FailRequest(messageText: $"Processing failed: " + ex.GetBaseException().Message);
//        manager.Save();
//    }

//    swatch.Stop();
//    QueueMonitorServiceHub.WriteMessageInternal(item, elapsed: (int)swatch.ElapsedMilliseconds).FireAndForget();
//};
//controller.StartProcessingAsync();





void Controller_ExecuteStart(QueueMessageManager obj)
{
    throw new NotImplementedException();
}


//builder.Services.AddQueueHubAuthorization();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

if (qmmApp.Configuration.System.LiveReloadEnabled)
    app.UseLiveReload();

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
