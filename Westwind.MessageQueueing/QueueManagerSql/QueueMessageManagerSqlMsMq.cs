using System;
using System.Diagnostics;
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

        public QueueMessageManagerSqlMsMq()             
        {
            MsMqQueuePath = @".\private$\";
        }
    
        public QueueMessageManagerSqlMsMq(string connectionString) 
            : base(connectionString)
        {            
             MsMqQueuePath = @".\private$\";            
        }

        public QueueMessageManagerSqlMsMq(string connectionString, string queuePath)
            : base(connectionString)
        {
            MsMqQueuePath = queuePath ?? @".\private$\";
        }

        static object QueueCreateLock = new Object();

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
                lock (QueueCreateLock)
                {
                    if (MessageQueue.Exists(queueId))
                        queue = new MessageQueue(queueId);
                    else
                    {
                        // Create the Queue
                        queue = MessageQueue.Create(queueId);
                        //queue = new MessageQueue(queueId);
                        queue.Label = "Queue Message Manager for " + queueName;
                        queue.SetPermissions("EVERYONE", MessageQueueAccessRights.FullControl);
                        queue.SetPermissions("SYSTEM", MessageQueueAccessRights.FullControl);
                        queue.SetPermissions("NETWORK SERVICE", MessageQueueAccessRights.FullControl);
                        queue.SetPermissions("Administrators", MessageQueueAccessRights.FullControl);
                    }
                }
            }

            return queue;
        }

        /// <summary>
        /// Saves the passed message item or the attached item
        /// to the database and creates a MSMQ message for the ID
        /// to be picked up. 
        /// 
        /// Call this after updating properties
        /// or individual values.
        /// 
        /// Inserts or updates based on whether the ID exists
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool Save(QueueMessageItem item = null)
        {
            if (item == null)
                item = Item;

            bool isNew = false;
            if (item!= null)
                isNew = item.__IsNew;

            // first write the SQL record
            if (!base.Save(item))
                return false;

            if (!isNew)
                return true;

            // now write the MSMQ entry - if it fails
            // the queue item is removed
            return InsertIdIntoQueue(item);            
        }

        /// <summary>
        /// Helper method that inserts the 
        /// </summary>
        /// <returns></returns>
        public bool InsertIdIntoQueue(QueueMessageItem item = null)
        {
            if (item == null)
                item = Item;

            // write new entries into the queue
            var queue = GetQueue(item.QueueName);
            if (queue == null)
            {
                DeleteMessage(item.Id);
                return false;
            }

            try
            {
                queue.Formatter = new StringMessageFormatter();
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
                throw new InvalidOperationException("Unable to access MSMQ queue: " + MsMqQueuePath + "qmm_" + queueName.ToLower());

            Message msg = null;
            try
            {
                msg = queue.Receive(new TimeSpan(1));
            }
            catch (MessageQueueException ex)
            {
                if (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                    return null; // not an error - just exit

                SetError("Queue receive error: " + ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                SetError("Queue receive error: " + ex.Message);
                return null;
            }

            if (msg == null)
                return null;  // nothing waiting

            msg.Formatter = new StringMessageFormatter();
            var id = msg.Body;
            if (id == null)
                return null; // invalid key

            // now load the item
            var item = Load(id.ToString());
            if (item == null) {
                SetError("Queue item no longer exists.");
                return null;
            }
            
            item.__IsNew = false;
            item.Started = DateTime.UtcNow;
            item.Status = "Started";

            // load up Properties from XmlProperties field
            GetProperties("XmlProperties", Item);

            return item;
        }

        /// <summary>
        /// Resubmit message into the queue as a cleared and message
        /// to be reprocessed. All date flags are cleared.
        /// 
        /// This method immediately writes the queue item to disk
        /// immediately. This version also writes an MSMQ item for
        /// the ID.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool ResubmitMessage(QueueMessageItem item = null)
        {
            if (!base.ResubmitMessage(item))
                return false;

            return InsertIdIntoQueue(item);
        }

    }
}
