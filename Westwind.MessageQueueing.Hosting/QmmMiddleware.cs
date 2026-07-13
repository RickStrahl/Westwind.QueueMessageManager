using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        services.AddSingleton<
            IAuthorizationHandler,
            QueueMonitorAccessHandler>();

        services.AddAuthorization(options =>
        {
            // Register [QueueMonitorAuthorize] attribute
            options.AddPolicy("QueueMonitorAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new QueueMonitorAccessRequirement());
            });
        });


        //var inheritedRouteConvention = new InheritedControllerRouteConvention
        //{
        //    //ChildControllerTypes = [typeof(SampleAppQmmApiController)]
        //};
        //services.AddSingleton<IActionDescriptorProvider>(inheritedRouteConvention);

        var mvcBuilder = services.AddControllersWithViews()
            // have to let MVC know we have a dynamically loaded controller
            .AddApplicationPart(typeof(QmmApiController).Assembly)
            .AddNewtonsoftJson(opt =>
            {
                var env = services.BuildServiceProvider(false).GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                    opt.SerializerSettings.Formatting = Formatting.Indented;
            });

        services.AddSignalR();

        return services;
    }

    public static IApplicationBuilder UseQmm(this IApplicationBuilder builder)
    {
        var config = builder.ApplicationServices.GetRequiredService<QmmMiddlewareConfiguration>();

        if (config.Container != null)
        {
            QueueContainer.Current = config.Container;
            QueueContainer.Current.StartProcessingAsync();
        }
        else
            throw new ApplicationException("QMM QueueContainer is not configured. Please configure the QueueContainer before starting the application.");



        return builder;
    }
}


public class QmmMiddlewareConfiguration
{
    public static QmmMiddlewareConfiguration Current { get; set; }

    public QueueContainer Container { get; set; }


    public void LoadContainerFromFile(string filename)
    {
        if (string.IsNullOrEmpty(filename) || !System.IO.File.Exists(filename))
            throw new ArgumentException("Invalid filename or file does not exist.", nameof(filename));

        try
        {
            Container = QueueContainer.CreateFromConfigurationFile(filename);
            QueueContainer.Current = Container;
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Error deserializing QueueContainer from file '{filename}': {ex.Message}", ex);
        }
    }

    public void SetContainer(QueueContainer container)
    {
        Container = container;
    }


    public void CreateContainer(string defaultConnectionString,
        int defaultThreadCount,
        int defaultWaitInterval,
        IEnumerable<QueueController> controllers
        )
    {
        if (controllers == null)
            throw new ArgumentException("Controllers cannot be null when creating a new QueueContainer.", nameof(controllers));

        Container = new QueueContainer
        {
            DefaultConnectionString = defaultConnectionString,
            DefaultThreadCount = defaultThreadCount,
            DefaultWaitInterval = defaultWaitInterval,
            Controllers = controllers.ToList()
        };
        
    }
}