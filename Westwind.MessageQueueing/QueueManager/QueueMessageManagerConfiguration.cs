using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// Poll interval for the controller in milliseconds
        /// when no requests are pending
        /// </summary>
        public int WaitInterval { get; set; }

        /// <summary>
        /// Specifies the default queue to look for
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// The number of threads that the Queue controller
        /// uses to process incoming queue requests
        /// </summary>
        public int ControllerThreads { get; set; }


        /// <summary>
        /// A list of controllers that can be launched automatically
        /// when QueueControllerMultiple is started
        /// </summary>
        public List<ControllerConfiguration> Controllers { get; set; }

        /// <summary>
        /// When hosting as a Service you can optionnally 
        /// host the SignalR Service to feed the Monitor Web
        /// interface from the service
        /// </summary>
        public string MonitorHostUrl { get; set; }

        /// <summary>
        /// Singleton instance of a Configuration Manager.
        /// Can be used globally to access a single 
        /// Queue Configuration.
        /// </summary>
        public static QueueMessageManagerConfiguration Current { get; private set; }
        

        public QueueMessageManagerConfiguration()
        {
            ConnectionString = "QueueMessageManager";
            WaitInterval = 1000;
            ControllerThreads = 1;
            QueueName = string.Empty;
            MonitorHostUrl = "http://*:8080/";
        }


        static QueueMessageManagerConfiguration()
        {
            Current = new QueueMessageManagerConfiguration();
            Current.Initialize(sectionName: "QueueManagerConfiguration");
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
    }

    public class ControllerConfiguration
    {
        public string ConnectionString { get; set; }
        public string QueueName { get; set;  }
        public int ControllerThreads { get; set; }
        public int WaitInterval { get; set; }

        /// <summary>
        /// Allows retrieving an object from a string generated with ToString()
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ControllerConfiguration FromString(string data)
        {
            return StringSerializer.Deserialize<ControllerConfiguration>(data, ",");
        }

        public override string ToString()
        {
            return StringSerializer.SerializeObject(this, ",");
        }
    }
}
