using Microsoft.AspNetCore.Mvc.Razor;
using Westwind.Utilities.Configuration;
using IConfigurationProvider = Westwind.Utilities.Configuration.IConfigurationProvider;

namespace Westwind.QueueManager.Hosting;

public class qmmAppConfiguration : AppConfiguration
{
   
    public string ConnectionString { get; set; } =
        "server=.;database=QueueMessageManager;integrated security=yes;encrypt=false;trustservercertificate=true;";

    public bool AutoCreateTables { get; set; } = false;

    public string JwtIssuer { get; set; } = "Westwind.Web.Template";

    public string JwtAudience { get; set; } = "Westwind.Web.Template.Client";

    public string JwtSigningKey { get; set; } = "Westwind.Web.Template.Jwt.Signing.Key.Change.Me.2026";

    public string DefaultCulture { get; set; } = "en-US";

    public string ApplicationName { get; set; } = "Queue Message Manager";

    public string ApplicationShortName { get; set; } = "QMM";

    public string Theme { get; set; } = "Light";


    public EmailConfiguration Email { get; set; } = new();


    
    public SystemConfiguration System { get; set; } = new();
    

    protected override IConfigurationProvider OnCreateDefaultProvider(string sectionName, object configData)
    {
        return new JsonFileConfigurationProvider<qmmAppConfiguration>
        {
            JsonConfigurationFile = "_qmmApp-configuration.json"
        };
    }
}

public class EmailConfiguration
{
    public string MailServer { get; set; } = "localhost";
    public string MailServerUsername { get; set; } = string.Empty;
    public string MailServerPassword { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "no-reply@localhost";
    public string SenderName { get; set; } = "West Wind Web Template";
    public bool UseTls { get; set; }
    public string CcList { get; set; } = string.Empty;
    public string AdminCcList { get; set; } = string.Empty;
    public bool SendAdminEmails { get; set; }
    public bool SendEmails { get; set; }
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
