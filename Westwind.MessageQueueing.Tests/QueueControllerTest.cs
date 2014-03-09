using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using Westwind.MessageQueueing;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Tests
{
    [TestClass]
    public class QueueControllerTests
    {


        [TestMethod]
        public void SingleQueueControllerTest()
        {
            var manager = new QueueMessageManagerSql();

            // sample - create 3 message
            for (int i = 0; i < 3; i++)
            {
                var item = new QueueMessageItem()
                {
                    Type = "Queue1",
                    Message = "Print Image",
                    Action = "PRINTIMAGE",
                    TextInput = "4334333" // image Id
                };

                // sets appropriate settings for submit on item
                manager.SubmitRequest(item);

                // item has to be saved
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added " + manager.Item.Id);
            }

            Console.WriteLine("Starting... Async Manager Processing");

            // create a new Controller to process in the background
            // on separate threads
            var controller = new QueueController()
            {
                ThreadCount = 2 ,
                WaitInterval = 200,
                QueueName = "Queue1"
            };
            Console.WriteLine("Wait: " + controller.WaitInterval);

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


        [TestMethod]
        public void MultiQueueControllerTest()
        {
            var manager = new QueueMessageManagerSql();

            // sample - create 3 message in 'default' queue
            for (int i = 0; i < 3; i++)
            {
                var item = new QueueMessageItem()
                {
                    Message = "Print Image",
                    Action = "PRINTIMAGE",
                    TextInput = "4334333", // image Id
                    Type="Queue1"
                };

                // sets appropriate settings for submit on item
                manager.SubmitRequest(item);

                // item has to be saved
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added to Queue1:" + manager.Item.Id);
            }

            // sample - create 3 message in 'default' queue
            for (int i = 0; i < 3; i++)
            {
                var item = new QueueMessageItem()
                {
                    Message = "Print Image (2nd)",
                    Action = "PRINTIMAGE",
                    TextInput = "5334333", // image Id
                    Type = "Queue2"
                };

                // sets appropriate settings for submit on item
                manager.SubmitRequest(item);

                // item has to be saved
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added to Queue2: " + manager.Item.Id);
            }

            Console.WriteLine("Starting... Async Manager Queue1 Processing");

            // create a new Controller to process in the background
            // on separate threads
            var controller = new QueueController()
            {
                QueueName = "Queue1"
            };
            Console.WriteLine("Wait: " + controller.WaitInterval);

            // ExecuteStart Event is where your processing logic goes
            controller.ExecuteStart += controller_ExecuteStart;

            // ExecuteFailed and ExecuteComplete let you take actions on completion
            controller.ExecuteComplete += controller_ExecuteComplete;
            controller.ExecuteFailed += controller_ExecuteFailed;

            // actually start the queue
            controller.StartProcessingAsync();

            // second configuration for second queue
            var config = QueueMessageManagerConfiguration.CreateConfiguration();
            config.QueueName = "Queue2";

            var controller2 = new QueueController()
            {
                QueueName = "Queue2"
            };
            Console.WriteLine("Wait: " + controller.WaitInterval);

            // ExecuteStart Event is where your processing logic goes
            controller2.ExecuteStart += controller_ExecuteStart;

            // ExecuteFailed and ExecuteComplete let you take actions on completion
            controller2.ExecuteComplete += controller_ExecuteComplete;
            controller2.ExecuteFailed += controller_ExecuteFailed;

            // actually start the queue
            controller2.StartProcessingAsync();


            // For test we have to keep the threads alive 
            // to allow the 3 requests to process
            Thread.Sleep(3200);

            // shut down
            controller.StopProcessing();
            controller2.StopProcessing();

            Thread.Sleep(200);

            Console.WriteLine("Stopping... Async Manager Processing");
            Assert.IsTrue(true);
        }


        /// <summary>
        /// This test demonstrates the QueueControllerMultiple
        /// which allows loading up multiple queue processors
        /// and start them running simultaneously side by side.
        /// </summary>
        [TestMethod]
        public void QueueControllerMultipleTest()
        {
            var manager = new QueueMessageManagerSql();

            // sample - create 3 message in 'default' queue
            for (int i = 0; i < 3; i++)
            {
                var item = new QueueMessageItem()
                {
                    Message = "Print Image",
                    Action = "PRINTIMAGE",
                    TextInput = "4334333", // image Id
                    Type = "Queue1"
                };

                // sets appropriate settings for submit on item
                manager.SubmitRequest(item);

                // item has to be saved
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added to Queue1:" + manager.Item.Id);
            }

            // sample - create 3 message in 'default' queue
            for (int i = 0; i < 3; i++)
            {
                var item = new QueueMessageItem()
                {
                    Message = "Print Image (2nd)",
                    Action = "PRINTIMAGE",
                    TextInput = "5334333", // image Id
                    Type = "Queue2"
                };

                // sets appropriate settings for submit on item
                manager.SubmitRequest(item);

                // item has to be saved
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
                Console.WriteLine("added to Queue2: " + manager.Item.Id);
            }

            

            // create a new Controller to process in the background
            // on separate threads
            var controller = new QueueControllerMultiple(new List<QueueController>()
            {
                new QueueControllerMultiple()
                {
                    QueueName = "Queue1", 
                    WaitInterval = 300,
                    ThreadCount = 5
                },
                new QueueControllerMultiple()
                {
                    QueueName = "Queue2",
                    WaitInterval = 500, 
                    ThreadCount = 3
                }
            });

            // Point all controllers at the same execution handlers
            // Alternately you can configure each controller with their
            // own event handlers or implement custom controller subclasses
            // that use the OnXXX handlers to handle the events
            controller.ExecuteStart += controller_ExecuteStart;
            controller.ExecuteComplete += controller_ExecuteComplete;
            controller.ExecuteFailed += controller_ExecuteFailed;
            
            // actually start the queue
            Console.WriteLine("Starting... Async Manager Processing");

            controller.StartProcessingAsync();

            // For test we have to keep the threads alive 
            // to allow the 10 requests to process
            Thread.Sleep(3000);

            // shut down
            controller.StopProcessing();
            
            Thread.Sleep(200);

            Console.WriteLine("Stopping... Async Manager Processing");
            Assert.IsTrue(true);

            Console.WriteLine("Processed: " + controller.MessagesProcessed);
        }

        public int RequestCount = 0;

        /// <summary>
        /// This is where your processing happens
        /// </summary>
        /// <param name="manager"></param>
        private void controller_ExecuteStart(QueueMessageManager manager)
        {
            // get active queue item
            var item = manager.Item;

            // Typically perform tasks based on some Action/request
            if (item.Action == "PRINTIMAGE")
            {
                // recommend you offload processing
                //PrintImage(manager);                
            }
            else if (item.Action == "RESIZETHUMBNAIL")
            {
                //ResizeThumbnail(manager);
            }

            // just for kicks
           Interlocked.Increment(ref RequestCount);

            // every other request should throw exception, trigger ExecuteFailed            
            if (RequestCount % 2 == 0)
            {
                // Execption:
                object obj = null;
                obj.ToString();
            }

            // Complete request 
            manager.CompleteRequest(messageText: "Completed request " + DateTime.Now,
                                    autoSave: true);            
        }
        private void controller_ExecuteComplete(QueueMessageManager manager)
        {
            // grab the active queue item
            var item = manager.Item;

            // Log or otherwise complete request
            Console.WriteLine(item.Id + " - " + item.Type +  " - Item Completed");
        }
        private void controller_ExecuteFailed(QueueMessageManager manager, Exception ex)
        {
            Console.WriteLine("Failed (on purpose): " + manager.Item.Type + " - " +  manager.Item.Id + " - " + ex.Message);
        }
    }
}
