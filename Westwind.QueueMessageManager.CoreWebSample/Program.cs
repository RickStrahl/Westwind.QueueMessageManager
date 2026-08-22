using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

using System.Runtime.InteropServices;
using Westwind.AspNetCore.Errors;
using Westwind.AspNetCore.LiveReload;
using Westwind.MessageQueueing;
using Westwind.MessageQueueing.Hosting;
using Westwind.QueueMessageManager.CoreWebSample;
using Westwind.Utilities;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;


qmmApp.IsDevelopment = builder.Environment.IsDevelopment();
qmmApp.EnvironmentName = builder.Environment.EnvironmentName;
qmmApp.Constants.StartupFolder = Environment.CurrentDirectory;
qmmApp.Constants.WebRootFolder = Path.Combine(qmmApp.Constants.StartupFolder, "wwwroot");

var configFile = "_qmm-app-config.json";
var configExists = File.Exists(configFile);


var qmmConfig = qmmApp.Configuration;
builder.Configuration.GetSection("qmmApp").Bind(qmmConfig);
services.AddSingleton(qmmConfig);
if (Environment.CommandLine.Contains("-createdb", StringComparison.OrdinalIgnoreCase))
{
    var manager = new QueueMessageManagerSql(qmmConfig.ConnectionString);
    if (manager.CreateDatastore())
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Database has been created successfully (or it exists already).");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Failed to create the database.");
        Console.ResetColor();
    }
    return;
}

var appConfig = SampleApp.Configuration;
builder.Configuration.GetSection("sampleApp").Bind(appConfig);
services.AddSingleton(appConfig);

// Code Configuration
services.AddQmm(options =>
{
    if(configExists)
        options.LoadContainerFromFile("_qmm-app-config.json");
    else
    {
        options.SetContainer(new QueueContainer
        {
            DefaultConnectionString = qmmApp.Constants.DefaultConnectionString, 
            DefaultThreadCount =1, 
            DefaultWaitInterval = 300,
            Controllers = [
                    new QueueController {
                        ConnectionString = qmmApp.Constants.DefaultConnectionString,
                        QueueName = "Test1", 
                        ThreadCount = 1,
                        WaitInterval = 200,
                        QueueControllerTypeName =  "Westwind.QueueMessageManager.CoreWebSample.Test1Queue"
                    },
                    new QueueController {
                        QueueName = "Test2",                        
                        QueueControllerTypeName =  "Westwind.QueueMessageManager.CoreWebSample.Test2Queue"
                    },
                    new QueueController {
                        QueueName = "Test1",                       
                        QueueControllerTypeName =  "Westwind.QueueMessageManager.CoreWebSample.Test1Queue"
                    },
                ]
        });
    }
});

// write out config file if it doesn't exist
// write after 
if (!configExists)
{
    qmmConfig.Write();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Configuration file '{configFile}' was created. Review it, and set default values, and restart the application.");
    Console.ResetColor();
    return;
}


if (appConfig.System.LiveReloadEnabled)
{
    var mvcBuilder = services.AddControllersWithViews();
    mvcBuilder.AddRazorRuntimeCompilation();

    services.AddLiveReload(config =>
    {
        config.LiveReloadEnabled = appConfig.System.LiveReloadEnabled;
        config.RefreshInclusionFilter = path =>
        {
            if (path.Contains("/LocalizationAdmin", StringComparison.OrdinalIgnoreCase))
                return RefreshInclusionModes.DontRefresh;

            return RefreshInclusionModes.ContinueProcessing;
        };
    });
}


//builder.Services.AddQueueHubAuthorization();


var app = builder.Build();



// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//}

if (appConfig.System.LiveReloadEnabled)
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
if (appConfig.System.ErrorDisplayMode == ErrorDisplayModes.Developer)
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
