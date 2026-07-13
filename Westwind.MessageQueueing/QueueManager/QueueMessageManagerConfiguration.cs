using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Westwind.Utilities;
using Westwind.Utilities.Configuration;

namespace Westwind.MessageQueueing
{    


    public class QueueMessageManagerConfiguration : AppConfiguration
    {
        /// <summary>
        /// The connection string or connection string name
        /// that is used for database access from the 
        /// Queue Manager
        /// </summary>
        public string ConnectionString { get; set; }


        /// <summary>
        /// If true the database will be created automatically if it doesn't exist.
        /// Adds a little overhead on creation of each manager instance.
        /// </summary>
        public bool AutoCreateTables { get; set; } = false;

        /// <summary>
        /// Poll interval for the controller in milliseconds
        /// when no requests are pending
        /// </summary>
        public int WaitInterval { get; set; }

        /// <summary>
        /// The default queue name that is assigned to queues if 
        /// no value is assigned. Defaults to null/empty (ie. no name)
        /// 
        /// This value is assigned to the QueueName property of the manager
        /// </summary>
        public string DefaultQueueName { get; set; }


        /// <summary>
        /// Specifies the default queue to look for
        /// </summary>
        public string DefaultControllerQueueName { get; set; }

        /// <summary>
        /// The number of threads that the Queue controller
        /// uses to process incoming queue requests
        /// </summary>
        public int DefaultThreadCount { get; set; }

       

        /// <summary>
        /// A list of controllers that can be launched automatically
        /// when QueueControllerMultiple is started. Note this
        /// property is available only to the multi-controller implementation.
        /// Otherwise use the QueueName and WaitInterval properties
        /// to modify operation of an individual queue.
        /// </summary>
        public List<ControllerConfiguration> Controllers { get; set; }

        /// <summary>
        /// The URL where SignalR accepts requests on.
        /// Typically this will be ~/signalr
        /// </summary>
        public string MonitorSignalRHubUrl { get; set; }

        /// <summary>
        /// The URL to the Monitor's HTML Page that 
        /// displays the monitor. ~/QueueMonitor.cshtml
        /// </summary>
        public string MonitorHtmlUrl { get; set;  }

        /// <summary>
        /// When self-hosting as a Service you can optionnally 
        /// host the SignalR Service to feed the Monitor Web
        /// interface from the service.
        /// Typically: http://*:8080/ or http://144.12.121.1:8080/
        /// </summary>
        public string MonitorHostUrl { get; set; }


        /// <summary>
        /// Link displayed on the Queue Monitor page that links
        /// back to an external URL on the Host site.
        /// </summary>
        public string MonitorReferringSiteUrl { get; set; }


        /// <summary>
        /// Singleton instance of a Configuration Manager.
        /// Can be used globally to access a single 
        /// Queue Configuration.
        /// Used only by Client when 
        /// </summary>
        public static QueueMessageManagerConfiguration Current { get; private set; }
        

        public QueueMessageManagerConfiguration()
        {
            ConnectionString =  "Server=.;Database=QueueMessageManager;integrated security=true;Enlist=True;MultipleActiveResultSets=True;Encrypt=False";
            WaitInterval = 1000;
            DefaultThreadCount = 1;
            DefaultControllerQueueName = string.Empty;
            MonitorHostUrl = "http://*:5080/";
            MonitorSignalRHubUrl = "~/signalR";
            MonitorHtmlUrl = "~/QueueMonitor.cshtml";
            Controllers = [];
        }


        static QueueMessageManagerConfiguration()
        {
            Current = new QueueMessageManagerConfiguration();
            Current.Initialize();
        }

        /// <summary>
        /// Creates a new instance of a configuration object that's
        /// copied from the stock configuration.
        /// 
        /// Use this if you need to create multiple configurations
        /// for multiple Controllers running at the same time.
        /// </summary>
        /// <returns></returns>
        public static QueueMessageManagerConfiguration CreateConfiguration()
        {
            var manager = new QueueMessageManagerConfiguration();
            DataUtils.CopyObjectData(Current, manager);
            return manager;
        }

        protected override IConfigurationProvider OnCreateDefaultProvider(string fileName, object configData)
        {

           var jsonFile = "qmm-config.json";
            
            var provider = new JsonFileConfigurationProvider<QueueMessageManagerConfiguration>()
            {
                JsonConfigurationFile = jsonFile,                                   
            };            
            
            return provider;
        }
    }

    /// <summary>
    /// Indidual Controller Configuration Item
    /// in a multi-controller configuration.        
    /// </summary>
    public class ControllerConfiguration 
    {
        /// <summary>
        /// Connection string for the database or queue data backend.
        /// </summary>
        public string ConnectionString { get; set; } 

        /// <summary>
        /// Name of the queue - can be empty or null
        /// </summary>
        public string QueueName { get; set; } 

        /// <summary>
        /// Number of threads used for this controller in
        /// threading mode.
        /// </summary>
        public int ControllerThreads { get; set; } = 1;

        /// <summary>
        /// Time to wait between before next request check
        /// </summary>
        public int WaitInterval { get; set; } = 300;


        /// <summary>
        /// The type that is used to create the Queue Message Manager
        /// </summary>
        public string QueueManagerType { get; set; } = nameof(QueueMessageManagerSql);            

        /// <summary>
        /// Allows retrieving an object from a string generated with ToString()
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ControllerConfiguration FromString(string data)
        {
            return StringSerializer.Deserialize<ControllerConfiguration>(data, ",");
        }

        /// <summary>
        /// Creates a serialized string of properties
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return StringSerializer.SerializeObject(this, ",");
        }
    }


    /// <summary>
    /// Determines how timed out messages are handled. Default is Timeout
    /// </summary>
    public enum TimeoutActions
    {
        Timeout,
        Delete,
        Reset,
        Fail,
        Cancel        
    }
}
