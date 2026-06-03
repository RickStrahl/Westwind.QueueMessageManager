using System.Reflection;
using Westwind.Utilities;

namespace Westwind.QueueManager.CoreWebSample;

public static class qmmApp
{
    public static qmmAppConfiguration Configuration { get; set; }

    public static qmmAppConstants Constants { get; set; } = new();

    public static bool IsDevelopment { get; set; }

    public static string EnvironmentName { get; set; } = "Production";

    public static string Version
    {
        get
        {
            if (field == null)
                field = Assembly.GetEntryAssembly().GetName().Version.FormatVersion(2, 4);

            return field;
        }
    }

    static qmmApp()
    {
        Configuration = new qmmAppConfiguration();
        Configuration.Initialize();
    }

    public static string NewId()
    {
        return DataUtils.GenerateUniqueId(10);
    }
}

public class qmmAppConstants
{
    public string DefaultConnectionString { get; set; } =
        "server=.;database=QueueMessageManager;integrated security=yes;encrypt=false;trustservercertificate=true;encrypt=false";


    public string StartupFolder { get; set; } = string.Empty;

    public string WebRootFolder { get; set; } = string.Empty;
}
