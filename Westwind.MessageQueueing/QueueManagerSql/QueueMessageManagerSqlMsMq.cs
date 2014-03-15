using System;
using System.Messaging;

namespace Westwind.MessageQueueing
{
    /// <summary>
    /// An implementation of a combination of SQL Server and MSMQ to handle
    /// two messaging via random acccess to messages so they can be retrived
    /// for long running tasks where both client and server can interact
    /// with each message for processing.
    /// 
    /// This implementation uses SQL server for the actual data storage and
    /// MSMQ to handle the message de-queuing by storing IDs in MSMQ. MSMQ
    /// allows much greater throughput for dequeuing message ids when polled
    /// frequently.
    /// 
    /// Great for long running tasks or even light workflow scenarios.
    /// </summary>       
    public class QueueMessageManagerSqlMsMq : QueueMessageManagerSql, IDisposable
    {
        public string MsMqQueuePath { get; set; }

        public QueueMessageManagerSqlMsMq() : base()
        {
            MsMqQueuePath = @".\private$\";
        }
    
        public QueueMessageManagerSqlMsMq(string connectionString, string queuePath = null) : base(connectionString)
        {            
             MsMqQueuePath = queuePath ?? @".\private$\";
        }

        /// <summary>
        /// Creates an MSMQ Queue
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public MessageQueue GetQueue(string queueName = null)
        {
            if (queueName == null)
                queueName = string.Empty;

            string queueId = MsMqQueuePath + "QMM_" + queueName;

            MessageQueue queue;
            if (MessageQueue.Exists(queueId))
                queue = new MessageQueue(queueId);
            else
            {
                // Create the Queue
                MessageQueue.Create(queueId);
                queue = new MessageQueue(queueId);
                queue.Label = "Queue Message Manager for " + queueName;
            }

            return queue;
        }

        /// <summary>
        /// Saves the passed message item or the attached item
        /// to the database. Call this after updating properties
        /// or individual values.
        /// 
        /// Inserts or updates based on whether the ID exists
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool Save(QueueMessageItem item = null)
        {
            if (!base.Save(item))
                return false;

            if (item == null)
                item = Item;

            var queue = GetQueue(item.QueueName);
            if (queue == null)
            {
                DeleteMessage(item.Id);
                return false;
            }

            try
            {
                queue.Send(Item.Id);
            }
            catch (Exception ex)
            {
                SetError(ex);
                DeleteMessage(item.Id);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves the next pending Message from the Queue based on a provided queueName
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns>item or null. Null can be returned when there are no items or when there is an error</returns>
        public override QueueMessageItem GetNextQueueMessage(string queueName = null)
        {
            if (queueName == null)
                queueName = DefaultQueue;

            var queue = GetQueue(queueName);
            if (queue == null)
                throw new InvalidOperationException("Unable to access MSMQ queue: " + MsMqQueuePath + "QMM_" + queueName);

            var msg = queue.Receive(new TimeSpan(0,0,0,0,1));
            if (msg == null)
                return null;  // nothing waiting

            msg.Formatter = new XmlMessageFormatter(new Type[] {typeof (string)});
            var id = msg.Body;
            if (id == null)
                return null; // invalid key

            // now load the item
            return Load(id.ToString());
        }


    }
}
