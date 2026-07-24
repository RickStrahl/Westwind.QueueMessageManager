using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Hosting
{

    /// <summary>
    /// Hosted Service class that can stop the QUeueContainer and all related tasks
    /// </summary>
    public class QmmHostedService : IHostedService
    {                  
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            QueueContainer.Current.StopProcessing();            
            return Task.CompletedTask;
        }
    }
}
