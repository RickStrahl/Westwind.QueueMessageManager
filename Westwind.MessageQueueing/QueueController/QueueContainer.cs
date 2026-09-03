using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using Westwind.Utilities;

namespace Westwind.MessageQueueing;

/// <summary>
/// Class that acts as a container for multiple QueueControllers.
/// </summary>
public class QueueContainer : IDisposable
{
    /// <summary>
    /// A global instance of the QueueContainer that can be used across the application.    
    /// </summary>
    public static QueueContainer Current { get; set; }


    /// <summary>
    /// The default connection string for all controllers in the container **if**
    /// not specified explicitly by the controller.
    /// </summary>
    public string DefaultConnectionString { get; set; }

    /// <summary>
    /// The default wait interval for all controllers in the container **if** 
    /// not defined in the controller explicitly.
    /// </summary>
    public int DefaultWaitInterval { get; set; } = 300;

    /// <summary>
    /// The default threads used for all controllers in the container **if** 
    /// not defined in the controller explicitly.
    /// </summary>
    public int DefaultThreadCount { get; set; } = 1;

    /// <summary>
    /// Determines whether tables are automatically created
    /// if they don't exists on the connection string.
    /// 
    /// Note: Database must exist before this will work
    /// </summary>
    public bool AutoCreateTables { get; set; } = false;

    /// <summary>
    /// The list of controllers that are part of this container.
    /// </summary>      
    public List<QueueController> Controllers { get; set; } = [];
 

   


    /// <summary>
    /// Adds a controller to the container. If the controller doesn't have 
    /// a connection string or wait interval defined, it will use the default 
    /// values from the container.
    /// </summary>
    /// <param name="controller">Controller instance</param>
    public void AddController(QueueController controller)
    {
        if (string.IsNullOrEmpty(controller.ConnectionString))
            controller.ConnectionString = DefaultConnectionString;
        if (controller.WaitInterval < 1)
            controller.WaitInterval = DefaultWaitInterval;
        if (controller.ThreadCount < 1)
            controller.ThreadCount = DefaultThreadCount;

            Controllers.Add(controller);
    }

    /// <summary>
    /// Starts up all queues
    /// </summary>
    public void StartProcessingAsync()
    {
        foreach (var ctrl in Controllers)
        {
            {
                if (string.IsNullOrEmpty(ctrl.ConnectionString))
                    ctrl.ConnectionString = DefaultConnectionString;
                if (ctrl.WaitInterval < 1)
                    ctrl.WaitInterval = DefaultWaitInterval;
                if (ctrl.ThreadCount < 1)
                    ctrl.ThreadCount = DefaultThreadCount;               

                ctrl.StartProcessingAsync();
            }
        }
    }

    
    /// <summary>
    /// Stops all queue requests from processing  and ends
    /// the thread holding the queue controllers.
    /// </summary>
    public void StopProcessing()
    {
        foreach (QueueController controller in Controllers)
        {
            controller.StopProcessing();
        }
    }

    public void PauseProcessing(bool pause = true)
    {
        foreach (var controller in Controllers)
            controller.Paused = pause;

    }

    public void Dispose()
    {
        StopProcessing();
        Thread.Sleep(50);
        
        foreach(var ctrl in Controllers)
            ctrl.Dispose();

        Controllers.Clear();
    }


    /// <summary>
    /// Deserializes a container instance from a file. Allows non-compilatble 
    /// 
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException"></exception>    
    public static QueueContainer CreateFromConfigurationFile(string filename)
    {
        if (string.IsNullOrEmpty(filename) || !System.IO.File.Exists(filename))
            return null;

        try
        {            
            var json = File.ReadAllText(filename);
            return CreateFromConfigurationString(json);
        }
        catch 
        {
            return null;
        }        
    }

    /// <summary>
    /// Deserializes a container instance from a JSON string.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException"></exception>
    public static QueueContainer CreateFromConfigurationString(string json)
    {
        if(string.IsNullOrEmpty(json))
            return null;

        // Read the configuration values from the config file's
        // Container instance of the config object
        QueueContainer container;
        try
        {
            JObject jobj = JObject.Parse(json);
            var jContainer = jobj["Container"];
            container = jContainer.ToObject<QueueContainer>();
        }
        catch
        {
            throw new InvalidCastException("Unable to deserialize QueueContainer from configuration file.");
        }

        // Create the physical Controller instances from the type name in the config
        for (int i = 0; i < container.Controllers.Count ; i++)
        {
            var controller = container.Controllers[i];
            var controllerTypename = controller.QueueControllerTypeName;

            var typedController = controller.CreateControllerInstanceFromString();
            //ReflectionUtils.CreateInstanceFromString(controllerTypename);
            if (typedController == null)
                throw new InvalidCastException("Unable to create QueueController of type [" + controllerTypename + "]");
            
            DataUtils.CopyObjectData(controller, typedController, excludedProperties: "QueueManagerType,LogManager");

            container.Controllers[i] = typedController;
        }

        return container;
    }
    
    /// <summary>
    /// Creates a controller instance from controller 'configuration' value by 
    /// creating a new instance of the Controller Type. 
    /// </summary>
    /// <param name="controllerConfig"></param>
    /// <param name="container"></param>
    /// <returns></returns>
    /// <exception cref="InvalidCastException"></exception>
    public QueueController CreateController(QueueController controllerConfig, QueueContainer container)
    {
        var typename = controllerConfig.QueueControllerTypeName?.ToString();
        if (string.IsNullOrEmpty(typename))
            return null;

        var controller = ReflectionUtils.CreateInstanceFromString(typename) as QueueController;
        if (controller == null)
            throw new InvalidCastException("Unable to create QueueController of type " + typename);

        string connectionString = controllerConfig.ConnectionString?.ToString();
        if (string.IsNullOrEmpty(connectionString))
            connectionString = container.DefaultConnectionString;
        string queueName = controllerConfig.QueueName;
        int threadCount = controllerConfig.ThreadCount;
        int waitInterval = controllerConfig.WaitInterval;
        bool paused = controllerConfig.Paused;


        if (!string.IsNullOrEmpty(connectionString))
            controller.ConnectionString = connectionString;
        if (!string.IsNullOrEmpty(queueName))
            controller.QueueName = queueName;
        controller.ThreadCount = threadCount;
        controller.WaitInterval = waitInterval;
        controller.Paused = paused;

        return controller;
    }




    /// <summary>
    /// Saves the current container configuration to file
    /// </summary>
    /// <param name="filename">File name to save to</param>
    /// <returns>success or failure</returns>
    public bool SaveToConfigurationFile(string filename = "_qmm-container-config.json")
    {
        return JsonSerializationUtils.SerializeToFile(this, filename, false, true, false);
    }

}
