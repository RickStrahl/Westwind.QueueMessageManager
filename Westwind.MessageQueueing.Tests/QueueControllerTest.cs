using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using Westwind.MessageQueueing;

namespace Westwind.MessageQueueing.Tests
{
    [TestClass]
    public class QueueControllerTests
    {
        [TestMethod]
        public void QueueControllerTest()
        {
            var manager = new QueueMessageManager();

            for (int i = 0; i < 3; i++)
            {                
                manager.SubmitRequest(messageText: "New Entry");
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added " + manager.Entity.Id);
            }

            Console.WriteLine("Starting... Async Manager Processing");

            var controller = new QueueController()
            {
                ThreadCount = 3
            };
            
            controller.ExecuteStart += controller_ExecuteStart;
            controller.ExecuteFailed += controller_ExecuteFailed;

            controller.StartProcessingAsync();
            
            // keep it alive for short processing burst
            Thread.Sleep(2000);

            // shut down
            controller.StopProcessing();

            Console.WriteLine("Stopping... Async Manager Processing");

        }

        public int RequestCount = 0;

        
        private void controller_ExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            Console.WriteLine("Failed (on purpose): " + manager.Entity.Id + " - " + ex.Message);
        }
        void controller_ExecuteStart(QueueMessageManager manager)
        {
            Interlocked.Increment(ref RequestCount);

            // last one should throw exception
            if (RequestCount > 2)
            {
                // Execption:
                object obj = null;
                obj.ToString();
            }

            manager.CompleteRequest(messageText: "Completed request " + DateTime.Now.ToString(), autoSave: true);

            Console.WriteLine(manager.Entity.Id + " - Item Completed");

        }
    }
}
