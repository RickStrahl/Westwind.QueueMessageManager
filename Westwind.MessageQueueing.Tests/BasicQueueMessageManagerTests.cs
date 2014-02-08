using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Transactions;

namespace Westwind.MessageQueueing.Tests
{
    /// <summary>
    /// Summary description for UnitTest2
    /// </summary>
    [TestClass]
    public class BasicQueueMessageManagerTests
    {
        public const string CONNECTION_STRING = "TestContext";

        /// <summary>
        /// Checks to see whether connection strings are set
        /// on the configuration
        /// </summary>
        [TestMethod]
        public void ConstructorOverrideTest()
        {

            var manager = new QueueMessageManager(CONNECTION_STRING);
            Console.WriteLine(manager.ConnectionString);
            Assert.IsTrue(manager.ConnectionString == CONNECTION_STRING);

            var config = new QueueMessageManagerConfiguration()
            {                 
                ConnectionString = "MyApplicationConnectionString"
            };
            manager = new QueueMessageManager(config);
            Console.WriteLine(manager.ConnectionString);
            Assert.IsTrue(manager.ConnectionString == "MyApplicationConnectionString");
        }

        [TestMethod]
        public void SubmitRequestWithPresetObjectTest()
        {
            string xml = "<doc><value>Hello</value></doc>";
            var manager = new QueueMessageManager();

                var msg = new QueueMessageItem()
                {
                    Type="MPWF",
                    Message = "Xml Message  @ " + DateTime.Now.ToString("t"),
                    Action = "NEWXMLORDER",                    
                    Xml = xml
                };
                manager.SubmitRequest(msg);
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        }

        [TestMethod]
        public void MyTestMethod()
        {
            var manager = new QueueMessageManager();
            int queueCount = 10;

            bool res = true;
            for (int i = 0; i < queueCount; i++)
            {
                var msg = new QueueMessageItem()
                {
                    Type = "MPWF",
                    Message = "Xml Message #" + i.ToString() + " @ " + DateTime.Now.ToString("t"),
                    Action = "NEWXMLORDER"                 
                };                

                manager.SubmitRequest(msg);

                res = manager.Save();
                if (!res)
                    break;
            }
            
        }


        [TestMethod]
        public void GetRecentMessagesTest()
        {
            using (var manager = new QueueMessageManager())
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
            var manager = new QueueMessageManager();

            string imageId = "10";
           
            // Create a message object
            // item contains many properties for pushing
            // values back and forth as well as a  few message fields
            var item = manager.NewEntity();
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
            var manager = new QueueMessageManager();
            manager.SubmitRequest(messageText: "New Entry with Properties");

            // add a custom property
            manager.Properties.Add("Time", DateTime.Now);

            Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        }

        [TestMethod]
        public void LoadRequestTest()
        {
            var manager = new QueueMessageManager();
            var db = manager.Db;

            var item = db.Find<QueueMessageItem>("select TOP 1 * from queueMessageItems where IsComplete = 0");

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
            var manager = new QueueMessageManager();
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
            using (var manager = new QueueMessageManager())
            {
                // delete all pending requests
                int res = manager.Db.ExecuteNonQuery("delete from queuemessageItems where IsNull(started,'') = '' or started < '01/01/2000'");
                Console.WriteLine(res);

                manager.SubmitRequest(messageText: "Next Complete Test " + DateTime.Now.ToString("t"));
                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
            }

            using (var manager = new QueueMessageManager())
            {
                var item = manager.GetNextQueueMessage();
                Assert.IsNotNull(item, manager.ErrorMessage);

                Console.WriteLine(item.Message);

                manager.CompleteRequest(item, "Next Complete complete " + DateTime.Now.ToString("t"));

                Assert.IsTrue(manager.Save(), manager.ErrorMessage);
            }
        }

        [TestMethod]
        public void GetNextQueueMessageItemWithoutAddedItemTest()
        {
            // allow rolling back
            using (var scope = new TransactionScope())
            {
                using (var manager = new QueueMessageManager())
                {
                    // delete all pending requests
                    int res = manager.Db.ExecuteNonQuery("delete from queuemessageItems where IsNull(started,'') = '' or started < '01/01/2000'");
                    Console.WriteLine(res);

                    var item = manager.GetNextQueueMessage();
                    Assert.IsNull(item);

                    // Error Message should be: No queue messages pending
                    Console.WriteLine(manager.ErrorMessage);
                }
            }
        }



        [TestMethod]
        public void CompleteMessageTest()
        {

            this.SubmitRequestWithPropertiesTest();

            var manager = new QueueMessageManager();

            var item = new QueueMessageItem()
            {
                 TextInput = "My input",
                 Message = "Getting started."
            };
            manager.NewEntity(item);

            manager.Properties["Time"] = DateTime.Now;

            Assert.IsTrue(manager.Save(),manager.ErrorMessage);

            string reqId = item.Id;

            manager = new QueueMessageManager();            
            item = manager.GetNextQueueMessage();

            DateTime? time = manager.GetProperty("Time") as DateTime?;
            Assert.IsNotNull(time);

            manager.CompleteRequest(item,"Message completed @" + DateTime.Now.ToString("t"));

            Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        }

        [TestMethod]
        public void GetPendingMessagesTest()
        {
            var manager = new QueueMessageManager();
            
            var items = manager.GetPendingQueueMessages();

            Assert.IsNotNull(items);

            foreach (var item in items)
            {
                Console.WriteLine(item.Submitted + " - " + item.Id + " - " +  item.Message);
            }
        }

        [TestMethod]
        public void GetWaitingMessagesTest()
        {
            var manager = new QueueMessageManager();

            var items = manager.GetWaitingQueueMessages();

            Assert.IsNotNull(items);

            foreach (var item in items)
            {
                Console.WriteLine(item.Submitted + " - " + item.Id + " - " + item.Message);
            }
        }

        [TestMethod]
        public void GetWaitingMessagesCountTest()
        {
            var manager = new QueueMessageManager();

            int count = manager.GetWaitingQueueMessageCount();

            Assert.IsNotNull(count > -1);

            Console.WriteLine(count + " queued items waiting.");
        }

        [TestMethod]
        public void GetCompleteMessagesTest()
        {
            var manager = new QueueMessageManager();

            var items = manager.GetCompleteQueueMessages();

            Assert.IsNotNull(items);

            foreach (var item in items)
            {
                Console.WriteLine(item.Completed + " - " + item.Id + " - " + item.Message);
            }
        }

        [TestMethod]
        public void GetTimedoutMessagesTest()
        {
            var manager = new QueueMessageManager();

            var items = manager.GetTimedOutQueueMessages();

            Assert.IsNotNull(items);

            foreach (var item in items)
            {
                Console.WriteLine(item.Submitted + " - " + item.Id + " - " + item.Message);
            }
        }


        [TestMethod]
        public void ClearTimedoutMessages()
        {
            var manager = new QueueMessageManager();
            Assert.IsTrue(manager.ClearMessages(), manager.ErrorMessage);            
        }

        /// <summary>
        /// NOTE: This test only does something when the database tables are 
        /// non-existant. Otherwise it simply returns true.
        /// </summary>
        [TestMethod]
        public void CreateDataBaseTest()
        {
            var manager = new QueueMessageManager();            
            Assert.IsTrue(manager.CreateDatabaseTable(),manager.ErrorMessage);            
        }
    }
}
