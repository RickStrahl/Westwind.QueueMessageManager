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

            // sample - create 3 message
            for (int i = 0; i < 3; i++)
            {
                var item = new QueueMessageItem()
                {
                    Message = "Print Image",
                    Action = "PRINTIMAGE",
                    TextInput = "4334333" // image Id
                };

                // sets appropriate settings for submit on item
                manager.SubmitRequest(item);

                // item has to be saved
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added " + manager.Entity.Id);
            }

            Console.WriteLine("Starting... Async Manager Processing");

            // create a new Controller to process in the background
            // on separate threads
            var controller = new QueueController()
            {
                ThreadCount = 2
            };


            // ExecuteStart Event is where your processing logic goes
            controller.ExecuteStart += controller_ExecuteStart;

            // ExecuteFailed and ExecuteComplete let you take actions on completion
            controller.ExecuteComplete += controller_ExecuteComplete;
            controller.ExecuteFailed += controller_ExecuteFailed;

            // actually start the queue
            controller.StartProcessingAsync();

            // For test we have to keep the threads alive 
            // to allow the 3 requests to process
            Thread.Sleep(2000);

            // shut down
            controller.StopProcessing();

            Thread.Sleep(200);  

            Console.WriteLine("Stopping... Async Manager Processing");
            Assert.IsTrue(true);
        }

        public int RequestCount = 0;

        /// <summary>
        /// This is where your processing happens
        /// </summary>
        /// <param name="manager"></param>
        private void controller_ExecuteStart(QueueMessageManager manager)
        {
            // get active queue item
            var item = manager.Entity;

            // Typically perform tasks based on some Action/request
            if (item.Action == "PRINTIMAGE")
            {
                // recommend you offload processing
                //PrintImage(manager);
            }
            else if (item.Action == "RESIZETHUMBNAIL")
                //ResizeThumbnail(manager);

                // just for kicks
                Interlocked.Increment(ref RequestCount);

            // third request should throw exception, trigger ExecuteFailed            
            if (RequestCount > 2)
            {
                // Execption:
                object obj = null;
                obj.ToString();
            }

            // Complete request 
            manager.CompleteRequest(messageText: "Completed request " + DateTime.Now,
                                    autoSave: true);

            Console.WriteLine(manager.Entity.Id + " - Item Completed");
        }
        private void controller_ExecuteComplete(QueueMessageManager manager)
        {
            // grab the active queue item
            var item = manager.Entity;

            // Log or otherwise complete request
        }
        private void controller_ExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            Console.WriteLine("Failed (on purpose): " + manager.Entity.Id + " - " + ex.Message);
        }
    }
}
