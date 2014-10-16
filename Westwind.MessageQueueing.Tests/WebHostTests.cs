using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Westwind.MessageQueueing;
using Westwind.Utilities;

namespace QueueStarter.Tests
{
    [TestClass]
    public class WebHostTests
    {
        private string connectionString = "server=.;database=QueueManagerStarter;integrated security=true;";

        [TestMethod]
        public void CreateQmmTableTest()
        {
            var qm = new QueueMessageManagerSql(connectionString);

            Assert.IsNotNull(qm);

            Assert.IsTrue(qm.CreateDatastore(), qm.ErrorMessage);
        }

        [TestMethod]
        public void AddQueueItem()
        {
            var qm = new QueueMessageManagerSql(connectionString);

            var item = new QueueMessageItem()
            {
                Message = "Single SQL Entry",
                TextInput = "Process This",
                QueueName="MPWF",
                Action="HELLOWORLD",
                Xml = @"<doc>
    <company>West Wind</company>
    <name>Rick</name>    
</doc>
" 
            };
            Assert.IsTrue(qm.SubmitRequest(item, null, true), qm.ErrorMessage);
            
        }

        [TestMethod]
        public void AddManyQueueItems()
        {
            var qm = new QueueMessageManagerSql(connectionString);

            for (int i = 0; i < 10; i++)
            {
                var item2 = new QueueMessageItem()
                {
                    Message = "Sql Queue Entry",
                    TextInput = "Process This",
                    QueueName = "MPWF",
                    Action = "HELLOWORLD",
                    Xml = string.Format(@"<doc>
    <company>East Wind</company>
    <name>Rick</name> 
    <time>{0}</time>
</doc>
",DateTime.Now.ToString("MMM dd - HH:mm:ss"))
                };
                Thread.Sleep(300);

                Assert.IsTrue(qm.SubmitRequest(item2, null, true), qm.ErrorMessage);
            }
        }

        [TestMethod]
        public void AddManyQueueSqlMsMqItems()
        {
            var qm = new QueueMessageManagerSqlMsMq(connectionString);
            qm.MsMqQueuePath = @".\Private$\";

            for (int i = 0; i < 10; i++)
            {
                var item2 = new QueueMessageItem()
                {
                    Message = "MSMQ New Entry",
                    TextInput = "Process This",
                    QueueName = "MPWF",
                    Action = "HELLOWORLD",
                    Xml = string.Format(@"<doc>
    <company>East Wind</company>
    <name>Rick</name> 
    <time>{0}</time>
</doc>
", DateTime.Now.ToString("MMM dd - HH:mm:ss"))
                };
                Thread.Sleep(300);

                Assert.IsTrue(qm.SubmitRequest(item2, null, true), qm.ErrorMessage);
            }

            for (int i = 0; i < 10; i++)
            {
                var item2 = new QueueMessageItem()
                {
                    Message = "MSMQ New Entry",
                    TextInput = "Process This",
                    QueueName = "MPWF_VFP",
                    Action = "HELLOWORLD",
                    Xml = string.Format(@"<doc>
    <company>East Wind</company>
    <name>Rick</name> 
    <time>{0}</time>
</doc>
", DateTime.Now.ToString("MMM dd - HH:mm:ss"))
                };
                Thread.Sleep(300);

                Assert.IsTrue(qm.SubmitRequest(item2, null, true), qm.ErrorMessage);
            }
        }

        [TestMethod]
      public void GetNextQueueMessage()
      {
          var qm = new QueueMessageManagerSqlMsMq(connectionString);
            
          var item = qm.GetNextQueueMessage("MPWF");
          Console.WriteLine(qm.ErrorMessage);
          Console.WriteLine(JsonSerializationUtils.Serialize(item, true));
      }
    }
}
