using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Transactions;
using Westwind.Utilities;

namespace Westwind.MessageQueueing.Tests
{
    /// <summary>
    /// Summary description for UnitTest2
    /// </summary>
    [TestClass]
    public class BasicQueueMessageManagerSqlMsMqTests
    {
        public const string CONNECTION_STRING = "QueueMessageManager";
        public const string BASE_QUEUE_PATH = @".\private$\";

        /// <summary>
        /// Checks to see whether connection strings are set
        /// on the configuration
        /// </summary>
        [TestMethod]
        public void ConstructorOverrideTest()
        {

            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);
            Console.WriteLine(manager.ConnectionString);
            Assert.IsTrue(manager.ConnectionString == CONNECTION_STRING,"ConnectionString is not set");


            manager = new QueueMessageManagerSqlMsMq("MyApplicationConnectionString",BASE_QUEUE_PATH);
            Console.WriteLine(manager.ConnectionString);
            Assert.IsTrue(manager.ConnectionString == "MyApplicationConnectionString");
        }

        [TestMethod]
        public void SubmitRequestWithPresetObjectTest()
        {
            string xml = "<doc><value>Hello</value></doc>";
            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);

                var msg = new QueueMessageItem()
                {
                    QueueName="MPWF",
                    Message = "Xml Message  @ " + DateTime.Now.ToString("t"),
                    Action = "NEWXMLORDER",                    
                    Xml = xml
                };
                manager.SubmitRequest(msg);
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        }

        [TestMethod]
        public void SubmitRequestsToQueueTest()
        {
            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);
            int queueCount = 10;

            bool res = true;
            for (int i = 0; i < queueCount; i++)
            {
                var msg = new QueueMessageItem()
                {
                    QueueName = "MPWF",
                    Message = "Xml Message #" + i.ToString() + " @ " + DateTime.Now.ToString("t"),
                    Action = "NEWXMLORDER"                 
                };                

                manager.SubmitRequest(msg);

                res = manager.Save(msg);
                if (!res)
                    break;
            }
            
        }


        [TestMethod]
        public void GetRecentMessagesTest()
        {
            using (var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH))
            {
                var items = manager.GetRecentQueueItems();
                foreach (var item in items)
                {
                    Console.WriteLine(item.Id + " " + item.Message);
                }
            }          
        }


        [TestMethod]
        public void SubmitRequestTest()
        {
            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);

            string imageId = "10";
           
            // Create a message object
            // item contains many properties for pushing
            // values back and forth as well as a  few message fields
            var item = manager.CreateItem();
            item.Action = "PRINTIMAGE";
            item.TextInput = imageId;
            item.Message = "Print Image operation started at " + DateTime.Now.ToString();
            item.PercentComplete = 10;

            // *** you can also serialize objects directly into the Xml property
            // manager.Serialization.SerializeToXml(SomeObjectToSerialize);

            // add an arbitrary custom properties - serialized to Xml
            manager.Properties.Add("Time", DateTime.Now);
            manager.Properties.Add("User", "ricks");

            // Set the message status and timestamps as submitted             
            manager.SubmitRequest(item);

            // actually save the queue message to disk
            Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        }


        [TestMethod]
        public void SubmitRequestWithPropertiesTest()
        {
            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);
            manager.SubmitRequest(messageText: "New Entry with Properties");

            // add a custom property
            manager.Properties.Add("Time", DateTime.Now);

            Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        }

        [TestMethod]
        public void LoadRequestTest()
        {
            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);
            var db = manager.Db;

            var item = db.Find<QueueMessageItem>("select TOP 1 * from queueMessageItems where Started is null");

            // no pending items - nothing to do
            if (item == null)
            {
                Console.WriteLine("No pending items... nothing to do.");
                return;
            }

            string reqId = item.Id;

            // clear out item
            item = null;

            // load through manager
            item = manager.Load(reqId);

            Assert.IsNotNull(item, manager.ErrorMessage);

            item.Message = "Updated @ " + DateTime.Now.ToString("t");
            item.PercentComplete = 10;            
            
            Assert.IsTrue(manager.Save(), manager.ErrorMessage);            
        }

        [TestMethod]
        public void LoadRequestWithPropertiesTest()
        {
            var manager = new QueueMessageManagerSql();
            var db = manager.Db;

            var item = db.Find<QueueMessageItem>("select TOP 1 * from queueMessageItems where IsComplete = 0 and XmlProperties is not null");

            // no pending items - nothing to do
            if (item == null)
            {
                Console.WriteLine("No pending items... nothing to do.");
                return;
            }

            string reqId = item.Id;

            // clear out item
            item = null;

            // load through manager
            item = manager.Load(reqId);

            Assert.IsNotNull(item, manager.ErrorMessage);

            item.Message = "Updated @ " + DateTime.Now.ToString("t");
            item.PercentComplete = 10;

            // Update Properties
            object t = manager.GetProperty("Time");
            DateTime? time3 = t as DateTime?;

            Assert.IsNotNull(t, "Time Property is null and shouldn't be.");

            Assert.IsTrue(manager.Save(), manager.ErrorMessage);

            Console.WriteLine(item.XmlProperties);
        }

        [TestMethod]
        public void GetNextQueueMessageItemWithAddedItemTest()
        {
            using (var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH))
            {
                // delete all pending requests
                int res = manager.Db.ExecuteNonQuery("delete from queuemessageItems where started is null");
                Console.WriteLine(res);

                // delete all queued ids - otherwise no items will match :-)
                var queue = manager.GetQueue("");
                queue.Purge();

                // creates sql and msmq items
                manager.SubmitRequest(messageText: "Next Complete Test " + DateTime.Now.ToString("t"));
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
            }

            using (var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH))
            {
                var item = manager.GetNextQueueMessage();
                Assert.IsNotNull(item, manager.ErrorMessage);

                Console.WriteLine(item.Message);

                manager.CompleteRequest(item, "Next Complete complete " + DateTime.Now.ToString("t"));

                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
            }
        }


        [TestMethod]
        public void ScaleRetrievalTest()
        {
            string queueName = "Queue1";

            var manager = new QueueMessageManagerSqlMsMq(CONNECTION_STRING,BASE_QUEUE_PATH);

            manager.Db.ExecuteNonQuery("delete from queuemessageitems");
            manager.GetQueue(queueName).Purge();

            var sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < 50000; i++)
            {
                string imageId = "10";

                // Create a message object
                // item contains many properties for pushing
                // values back and forth as well as a  few message fields
                var item = manager.CreateItem();
                item.QueueName = queueName;
                item.TextInput = DataUtils.GenerateUniqueId(15);

                // Set the message status and timestamps as submitted             
                manager.SubmitRequest(item,autoSave: true);
            }

            Console.WriteLine("Insert time: " + sw.ElapsedMilliseconds);

            IdList = new List<string>();
            IdErrors = new List<string>();

            for (int i = 0; i < 50; i++)
            {
                var thread = new Thread(ProcessGetNextItem);
                thread.Start();
            }

            //Task.Run(() =>
            //{
            //    for (int i = 0; i < 100; i++)
            //    {
            //        manager = new QueueMessageManagerSql();

            //        string imageId = "10";

            //        // Create a message object
            //        // item contains many properties for pushing
            //        // values back and forth as well as a  few message fields
            //        var item = manager.CreateItem();
            //        item.QueueName = "Queue1";
            //        item.TextInput = DataUtils.GenerateUniqueId(15);

            //        // Set the message status and timestamps as submitted             
            //        manager.SubmitRequest(item, autoSave: true);
            //    }

            //    Thread.Sleep(60);
            //});

            for (int i = 0; i < 500; i++)
            {
                if (CancelProcessing)
                    break;

                string imageId = "10";

                // Create a message object
                // item contains many properties for pushing
                // values back and forth as well as a  few message fields
                var item = manager.CreateItem();
                item.QueueName = queueName;
                item.TextInput = DataUtils.GenerateUniqueId(15);

                // Set the message status and timestamps as submitted             
                manager.SubmitRequest(item, autoSave: true);

                Thread.Sleep(4);
            }


            Console.WriteLine("Waiting for 5 seconds");
            Thread.Sleep(5000);
            CancelProcessing = true;
            Thread.Sleep(100);

            Console.WriteLine("Done");

            Console.WriteLine("Items processed: " + IdList.Count);

            var grouped = IdList.GroupBy(s => s);
            Console.WriteLine("Unique Count: " + grouped.Count());

            foreach (var error in IdErrors)
                Console.WriteLine("  " + error);

        }

        private static object GetNextItemLock = new Object();
        private bool CancelProcessing = false;
        private List<string> IdList;
        private List<string> IdErrors;

        void ProcessGetNextItem()
        {
            while (!CancelProcessing)
            {
                var manager = new QueueMessageManagerSqlMsMq();
                var item = manager.GetNextQueueMessage("Queue1");
                if (item != null)
                    IdList.Add(item.Id);
                else
                    IdErrors.Add(manager.ErrorMessage);

                Thread.Yield();
            }
        }        
    }
}
