using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Runtime.InteropServices;
using Westwind.AspNetCore.Errors;
using Westwind.AspNetCore.LiveReload;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueManager.Hosting;
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



//var inheritedRouteConvention = new InheritedControllerRouteConvention
//{
//    //ChildControllerTypes = [typeof(SampleAppQmmApiController)]
//};
//services.AddSingleton<IActionDescriptorProvider>(inheritedRouteConvention);

//var mvcBuilder = services.AddControllersWithViews()
//    // have to let MVC know we have a dynamically loaded controller
//    .AddApplicationPart(typeof(QmmApiController).Assembly)
//    .AddNewtonsoftJson(opt =>
//    {
//        if (builder.Environment.IsDevelopment())
//            opt.SerializerSettings.Formatting = Formatting.Indented;
//    });
// services.AddSignalR();


services.AddQmm(options =>
{
    options.LoadContainerFromFile("_qmm-container-config.json");    
});



if (appConfig.System.LiveReloadEnabled)
{
    var mvcBuilder = services.AddControllersWithViews();
    mvcBuilder.AddRazorRuntimeCompilation();

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


//});

//QueueContainer queueContainer = null;
//bool readFromConfig = true;
//if (readFromConfig)
//{
//    // Load From File Config
//    queueContainer = QueueContainer.CreateFromConfigurationFile("qmm-container-config.json");
//}
//else
//{
//    // Explicitly load from Code
//    queueContainer = new Westwind.MessageQueueing.QueueContainer
//    {
//        DefaultConnectionString = qmmApp.Configuration.ConnectionString,
//        DefaultThreadCount = 1,
//        DefaultWaitInterval = 300,
//        Controllers = [
//            new Test1Queue() { ConnectionString = qmmApp.Configuration.ConnectionString, ThreadCount = 2 },
//            new Test2Queue() { ConnectionString = qmmApp.Configuration.ConnectionString, ThreadCount = 3, WaitInterval = 200 },
//            new Test2Queue() { ConnectionString = qmmApp.Configuration.ConnectionString, ThreadCount = 2, WaitInterval = 400 }
//        ]
//    };
//}
//queueContainer.StartProcessingAsync();

//queueContainer.SaveToConfigurationFile("qmm-container-config.json");



//builder.Services.AddQueueHubAuthorization();


var app = builder.Build();


//var adProvider = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
//foreach (var a in adProvider.ActionDescriptors.Items)
//{
//    var route = a.AttributeRouteInfo?.Template ?? "(conventional)";
//    Console.WriteLine($"{a.DisplayName} => {route}");
//}

//var endpointSource = app.Services.GetRequiredService<EndpointDataSource>();
//foreach (var endpoint in endpointSource.Endpoints.OfType<RouteEndpoint>())
//{
//    var methods = endpoint.Metadata
//        .OfType<HttpMethodMetadata>()
//        .FirstOrDefault()?.HttpMethods ?? new[] { "ANY" };

//    Console.WriteLine($"{string.Join(",", methods),-10} {endpoint.RoutePattern.RawText,-40} {endpoint.DisplayName}");
//}


// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//}

if (qmmApp.Configuration.System.LiveReloadEnabled)
    app.UseLiveReload();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(qmmApp.Constants.WebRootFolder)
});
// app.MapStaticAssets();   // TODO: What is this for?

app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
if (qmmApp.Configuration.System.ErrorDisplayMode == ErrorDisplayModes.Developer)
{
    app.UseDeveloperExceptionPage();
    ApiExceptionFilterAttribute.ShowExceptionDetail = true;
}
else
{
    app.UseExceptionHandler("/Home/Error");
}


app.UseQmm();

app.MapHub<QueueMonitorServiceHub>("/queueMonitorServiceHub");
QueueMonitorServiceHub.HubContext = app.Services.GetRequiredService<IHubContext<QueueMonitorServiceHub>>();




//app.MapGet("/api/test", () => {
//    return new { message = "Hello cruel World!" };
//});


//app.UseEndpoints(endpoints =>
//{
//    // We need MVC Routing for Markdown to work
//    endpoints.MapDefaultControllerRoute();
//});
// app.MapControllerRoute(
//     name: "default",
//     pattern: "{controller=Home}/{action=Index}/{id?}")
//     .WithStaticAssets();



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
