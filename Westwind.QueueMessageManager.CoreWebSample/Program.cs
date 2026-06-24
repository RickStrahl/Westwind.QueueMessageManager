using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Serilog;
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


QueueContainer queueContainer = null;
bool readFromConfig = true;
if (readFromConfig)
{
    // Load From File Config
    queueContainer = QueueContainer.CreateFromConfigurationFile("qmm-container-config.json");
}
else
{
    // Explicitly load from Code
    queueContainer = new Westwind.MessageQueueing.QueueContainer
    {
        DefaultConnectionString = qmmApp.Configuration.ConnectionString,
        DefaultThreadCount = 1,
        DefaultWaitInterval = 300,
        Controllers = [
            new Test1Queue() { ConnectionString = qmmApp.Configuration.ConnectionString, ThreadCount = 2 },
            new Test2Queue() { ConnectionString = qmmApp.Configuration.ConnectionString, ThreadCount = 3, WaitInterval = 200 },
            new Test2Queue() { ConnectionString = qmmApp.Configuration.ConnectionString, ThreadCount = 2, WaitInterval = 400 }
        ]
    };
}
queueContainer.StartProcessingAsync();

//queueContainer.SaveToConfigurationFile("qmm-container-config.json");



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
