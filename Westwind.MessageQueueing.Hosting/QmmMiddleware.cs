using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Westwind.MessageQueueing.Hosting;


public static class QmmMiddlewareExtensions
{


    public static IServiceCollection AddQmm(this IServiceCollection services,
        Action<QmmMiddlewareConfiguration> configAction = null)

    {
        var config = new QmmMiddlewareConfiguration();
        if (configAction != null)
            configAction.Invoke(config);

        QmmMiddlewareConfiguration.Current = config;
        services.AddSingleton(config);


        // Authorization builder related
        //services.AddSingleton<
        //    IAuthorizationHandler,
        //    QueueMonitorAccessHandler>();

        //services.AddAuthorization(options =>
        //{
        //    // Register [QueueMonitorAuthorize] attribute
        //    options.AddPolicy("QueueMonitorAccess", policy =>
        //    {
        //        policy.RequireAuthenticatedUser();
        //        policy.AddRequirements(new QueueMonitorAccessRequirement());
        //    });
        //});

        var mvcBuilder = services.AddControllersWithViews()
            // have to let MVC know we have a dynamically loaded controller with base handlers
            .AddApplicationPart(typeof(QmmApiController).Assembly)
            .AddNewtonsoftJson(opt =>
            {
                var env = services.BuildServiceProvider(false).GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                    opt.SerializerSettings.Formatting = Formatting.Indented;
            });

        // Queue Monitor
        if(!qmmApp.Configuration.DisableQueueMonitor)
            services.AddSignalR();

        // Shutdown processing
        services.AddHostedService<QmmHostedService>();

        return services;
    }

    public static IApplicationBuilder UseQmm(this IApplicationBuilder builder)
    {
        var config = builder.ApplicationServices.GetRequiredService<QmmMiddlewareConfiguration>();

        if (QueueContainer.Current != null)
        {            
            QueueContainer.Current.StartProcessingAsync();
        }
        else
            throw new ApplicationException("QMM QueueContainer is not configured. Please configure the QueueContainer before starting the application.");

        return builder;
    }
}


public class QmmMiddlewareConfiguration
{

    /// <summary>
    /// Global instance of the middleware configuration so we have access to the
    /// container uin
    /// </summary>
    public static QmmMiddlewareConfiguration Current { get; set; }

    //internal QueueContainer Container { get; set; }


    /// <summary>
    /// By default the QueueMonitor is enabled and runs. Use this option
    /// to explicitly disable the QueueMonitor and SignalR processing.
    /// </summary>
    public bool DisableQueueMonitor { get; set; } = true;



    /// <summary>
    /// Creates a global QueueContainer instance in QueueContainer.Current 
    /// from a configuration file. 
    /// </summary>
    /// <param name="filename">Path to the configuration file - default: "_qmm-container-config.json"</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public void LoadContainerFromFile(string filename = "_qmm-app-config.json")
    {
        if (string.IsNullOrEmpty(filename) || !System.IO.File.Exists(filename))
            throw new ArgumentException("Invalid filename or file does not exist.", nameof(filename));

        try
        {
            var container = QueueContainer.CreateFromConfigurationFile(filename);
            QueueContainer.Current = container;
            var controller = container.Controllers.FirstOrDefault();

            AutoCreateTables(controller);            
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Error deserializing QueueContainer from file '{filename}': {ex.Message}", ex);
        }
    }

    private void AutoCreateTables(QueueController controller)
    {
        if (!qmmApp.Configuration.AutoCreateTables) return;

        var managers = new HashSet<QueueMessageManager>();
        if (controller != null)
        {
            var manager = controller.CreateQueueMessageManager();
            if (!managers.Contains(manager))
            {
                manager.EnsureDataStoreExists();
                managers.Add(manager);
            }
        }
        foreach (var man in managers)
            man.Dispose();

        managers.Clear();
    }

    /// <summary>
    /// Configures the globally managed QueueContainer.Current instance 
    /// from a manually configured QueueContainer.
    /// </summary>
    /// <param name="container"></param>
    public void SetContainer(QueueContainer container)
    {        
        QueueContainer.Current = container;
    }


    /// <summary>
    /// Creates a new QueueContainer instance based on provided
    /// parameters.
    /// 
    /// Typically easier to just create a QueueContainer manually
    /// and set properties on it but this is a quick way to create
    /// an empty container.
    /// </summary>
    /// <param name="defaultConnectionString"></param>
    /// <param name="defaultThreadCount"></param>
    /// <param name="defaultWaitInterval"></param>
    /// <param name="controllers"></param>
    /// <exception cref="ArgumentException"></exception>
    public void CreateContainer(string defaultConnectionString,
        int defaultThreadCount,
        int defaultWaitInterval,
        IEnumerable<QueueController> controllers
        )
    {
        if (controllers == null)
            throw new ArgumentException("Controllers cannot be null when creating a new QueueContainer.", nameof(controllers));

        var container = new QueueContainer
        {
            DefaultConnectionString = defaultConnectionString,
            DefaultThreadCount = defaultThreadCount,
            DefaultWaitInterval = defaultWaitInterval,
            Controllers = controllers.ToList()
        };
        QueueContainer.Current = container;
        
    }
}