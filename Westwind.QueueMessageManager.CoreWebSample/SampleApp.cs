using Westwind.Utilities.Configuration;
using IConfigurationProvider = Westwind.Utilities.Configuration.IConfigurationProvider;

namespace Westwind.QueueMessageManager.CoreWebSample;


/// <summary>
/// Sample App Configuration and App Specific settings.
/// This is just for demonstration purposes to hold some 
/// app specific settings and behavior.
/// </summary>
public class SampleApp
{
    public static SampleAppConfiguration Configuration { get; set; }

    static SampleApp()
    {
        Configuration = new();           
        Configuration.Initialize();
    }
}


/// <summary>
/// Application Specific configuration.
/// 
/// Mainly used for system configuration settings 
/// of the Web app.
/// </summary>
public class SampleAppConfiguration : AppConfiguration
{
    public string ApplicationName { get; set; } = "Queue Message Manager";

    public string ApplicationShortName { get; set; } = "QMM";

    public SystemConfiguration System { get; set; } = new();


    protected override IConfigurationProvider OnCreateDefaultProvider(string sectionName, object configData)
    {
        return new JsonFileConfigurationProvider<SampleAppConfiguration>
        {
            JsonConfigurationFile = "_sample-app-config.json"
        };
    }
}

public class SystemConfiguration
{
    public bool RedirectToHttps { get; set; }
    public ErrorDisplayModes ErrorDisplayMode { get; set; } = ErrorDisplayModes.Application;
    public int CookieTimeoutDays { get; set; } = 2;
    public bool LiveReloadEnabled { get; set; }
}

public enum ErrorDisplayModes
{
    Application,
    ApplicationPlusDetail,
    Developer
}