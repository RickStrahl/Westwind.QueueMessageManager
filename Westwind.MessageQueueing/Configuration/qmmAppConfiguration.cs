using Westwind.Utilities.Configuration;

namespace Westwind.MessageQueueing;

public class qmmAppConfiguration : AppConfiguration
{
   
    public string ConnectionString { get; set; } =
        "server=.;database=QueueMessageManager;integrated security=yes;encrypt=false;trustservercertificate=true;";

    public bool AutoCreateTables { get; set; } = false;

    /// <summary>
    /// Disables the Queue Monitor by preventing the SignalR hub from starting and 
    /// QMM endpoints to work.
    /// </summary>
    public bool DisableQueueMonitor { get; set; } = false;

    public string ApplicationName { get; set; } = "Queue Message Manager";

    public string ApplicationShortName { get; set; } = "QMM";

    public string Theme { get; set; } = "Light";

    /// <summary>
    /// Queue Configuration
    /// </summary>
    public QueueContainer Container { 
        get => QueueContainer.Current;
        set => QueueContainer.Current = value;
    } 
    
    

    protected override IConfigurationProvider OnCreateDefaultProvider(string sectionName, object configData)
    {
        return new JsonFileConfigurationProvider<qmmAppConfiguration>
        {
            JsonConfigurationFile = "_qmm-app-config.json"
        };
    }
}