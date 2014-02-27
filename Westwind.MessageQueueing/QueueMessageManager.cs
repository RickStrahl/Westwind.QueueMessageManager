using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Westwind.MessageQueueing.Properties;
using System.Data;
using Westwind.Utilities;
using System.Collections.Generic;
using Westwind.Utilities.Data;
using System.Diagnostics;

namespace Westwind.MessageQueueing
{
    /// <summary>
    /// An implementation of a SQL based multi-access Queue
    /// that provides random acccess to requests so they can be retrived
    /// for long running tasks where both client and server can interact
    /// with each message for processing.
    /// 
    /// Great for long running tasks or even light workflow scenarios.
    /// </summary>    
    public abstract  class QueueMessageManager : IDisposable
    {
        protected const int INT_maxCount = 99999;
        protected bool _IsNew = false;

        /// <summary>
        /// Message Timeout. Messages are cleared
        /// with ClearMessages()
        /// </summary>
        public TimeSpan MessageTimeout { get; set; }

        /// <summary>
        ///  The name of the default queue that is accessed if
        ///  no queue name is specified
        /// </summary>
        public string DefaultQueue { get; set; }

        /// <summary>
        /// Holds the actual entity data for a message
        /// </summary>
        public QueueMessageItem Item { get; set; }

        /// <summary>
        /// Serialization Helper Methods to help serialize data to Xml and back
        /// easily
        /// </summary>
        public QueueMessageManagerSerializationHelper Serialization { get; set; }

        /// <summary>
        /// Instance of the configuration object for queuemessage manager
        /// </summary>
        public QueueMessageManagerConfiguration Configuration { get; set; }

        /// <summary>
        /// Error information about the last error that occurred
        /// when a method returns false
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Connection string used for this component
        /// </summary>
        public string ConnectionString { get; set; }

        public QueueMessageManager()
        {
            DefaultQueue = string.Empty;

            // 2 hours
            MessageTimeout = new TimeSpan(2, 0, 0);

            Serialization = new QueueMessageManagerSerializationHelper(this);
           
            Configuration = QueueMessageManagerConfiguration.Current;
            ConnectionString = Configuration.ConnectionString;
        }

        public QueueMessageManager(QueueMessageManagerConfiguration configuration) : this()
        {
            Configuration = configuration;
            ConnectionString = Configuration.ConnectionString;
        }

        public QueueMessageManager(string connectionString) : this()
        {
            ConnectionString = connectionString;
        }


        /// <summary>
        /// Loads a Queue Item
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract QueueMessageItem Load(string id);

        /// <summary>
        /// Retrieves the next pending Message from the Queue based on a provided type
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns>item or null. Null can be returned when there are no items or when there is an error</returns>
        public abstract QueueMessageItem GetNextQueueMessage(string queueName = null);
     
        /// <summary>
        /// Creates a new entity instance and properly
        /// initializes the instance's values.
        /// </summary>
        /// <returns></returns>
        public QueueMessageItem CreateItem(QueueMessageItem entity = null)
        {
            if (entity == null)
                Item = new QueueMessageItem();
            else
                Item = entity;

            Item.__IsNew = true;

            // ensure entity is properly configured
            Item.Submitted = DateTime.UtcNow;
            Item.Status = "Submitted";
            Item.Started = null;
            Item.Completed = null;

            return Item;
        }


        /// <summary>
        /// Saves the passed entity or the attached entity
        /// to the database. Call this after updating properties
        /// or individual values.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public abstract bool Save(QueueMessageItem entity = null);

        /// <summary>
        ///  Determines if anqueue has been completed
        /// successfully or failed.
        /// 
        /// Note this method returns true if the request
        /// has completed or cancelled/failed. It just
        /// checks completion.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract bool IsCompleted(string id = null);
        

        /// <summary>
        /// Sets the message properties for starting a new message request operation.
        /// Note the record is not written to the database use Save explicitly
        /// </summary>
        /// <param name="entity">An existing entity instance</param>
        public bool SubmitRequest(QueueMessageItem entity = null, string messageText = null, bool autoSave = false)
        {
            if (entity == null)
                entity = CreateItem();            

            entity.PercentComplete = 0;
            entity.Status = "Submitted";
            entity.Submitted = DateTime.UtcNow;
            entity.Started = null;
            entity.Completed = null;
            entity.IsComplete = false;
            entity.IsCancelled = false;

            if (entity.Type == null)
                entity.Type = this.DefaultQueue;

            if (messageText != null)
                entity.Message = messageText;

            Item = entity;

            if (autoSave)
                return Save();

            return true;
        }


        /// <summary>
        /// Sets the Item record with the required settings
        /// to complete a request. Note record is not written
        /// to database - Call Save explicitly.
        /// </summary>
        public bool CompleteRequest(QueueMessageItem entity = null, string messageText = null, bool autoSave = false)
        {
            if (entity == null)
                entity = Item;
            if (entity == null)
                entity = CreateItem();

            entity.PercentComplete = 100;
            entity.Status = "Completed";
            entity.Completed = DateTime.UtcNow;
            entity.IsComplete = true;
            entity.IsCancelled = false;

            if (messageText != null)
                entity.Message = messageText;

            if (autoSave)
                return Save();

            return true;
        }

        /// <summary>
        /// Sets the Item record with the required settings
        /// to complete and cancel a request. Not saved to database
        /// call Save() explicitly.
        /// </summary>
        public bool CancelRequest(QueueMessageItem entity = null, string messageText = null, bool autoSave = false)
        {
            if (entity == null)
                entity = Item;
            if (entity == null)
                entity = CreateItem();

            entity.Status = "Cancelled";
            entity.Completed = DateTime.UtcNow;
            entity.IsComplete = true;
            entity.IsCancelled = true;

            if (messageText != null)
                entity.Message = messageText;

            if (autoSave)
                return Save();

            return true;
        }

        /// <summary>
        /// Updates the QueueMessageStatus and or messages
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="status"></param>
        /// <param name="messageText"></param>
        /// <param name="percentComplete"></param>
        public bool UpdateQueueMessageStatus(QueueMessageItem entity = null, string status = null, string messageText = null, int percentComplete = -1, bool autoSave = false)
        {
            if (entity == null)
                entity = Item;
            if (entity == null)
                entity = CreateItem();

            if (!string.IsNullOrEmpty(status))
                entity.Status = status;

            if (!string.IsNullOrEmpty(messageText))
                entity.Message = messageText;

            if (percentComplete > -1)
                entity.PercentComplete = percentComplete;

            if (autoSave)
                return Save();

            return true;
        }


        /// <summary>
        /// Returns a list of recent queue items
        /// </summary>
        /// <param name="type"></param>
        /// <param name="itemCount"></param>
        /// <returns></returns>
        public abstract IEnumerable<QueueMessageItem> GetRecentQueueItems(string type = null, int itemCount = 25);


        /// <summary>
        /// Retrieves all messages that are pending, that have started
        /// but not completed yet. 
        /// </summary>
        /// <param name="queueName">Name of the queue to return items for</param>
        /// <param name="maxCount">Optional - max number of items to return</param>
        /// <returns></returns>
        public abstract IEnumerable<QueueMessageItem> GetPendingQueueMessages(string queueName = null, int maxCount = 0);


        /// <summary>
        /// Returns a count of messages that are waiting
        /// to be processed - this is the queue backup.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public abstract int GetWaitingQueueMessageCount(string queueName = null);


        /// <summary>
        /// Returns a count of messages that are waiting
        /// to be processed - this is the queue backup.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public abstract IEnumerable<QueueMessageItem> GetWaitingQueueMessages(string queueName = null, int maxCount = 0);

        /// <summary>
        /// Result Cursor: TCompleteMessages
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public abstract  IEnumerable<QueueMessageItem> GetCompleteQueueMessages(string queueName = null, int maxCount = 0);

        /// <summary>
        /// Returns a list of queue items that have timed out during processing.
        /// Not completed where started time is greater than the MessageTimeout.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public abstract IEnumerable<QueueMessageItem> GetTimedOutQueueMessages(string queueName = null, int maxCount = 0);

        /// <summary>
        /// Returns all messages in a queue that are cancelled
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public abstract IEnumerable<QueueMessageItem> GetCancelledMessages(string queueName = null, int maxCount = 0);

        /// <summary>
        /// Method used to clear out 'old' messages to keep database size down
        /// Removes messages that have been started but not completed in the
        /// specified timeout period.
        /// </summary>
        public abstract bool ClearMessages(TimeSpan? messageTimeout = null);


        /// <summary>
        /// Creates the DatabaseTable and stored procedure for the queue. Note this routine
        /// requires that a database exists already and uses the same connection string
        /// that is used for the main application.
        /// </summary>
        /// <returns></returns>
        public abstract bool CreateDatastore();

        #region ErrorHandling

        /// <summary>
        /// Clear the error messages
        /// </summary>
        public void SetError()
        {
            this.SetError("CLEAR");
        }


        /// <summary>
        /// Set error to the error message
        /// </summary>
        /// <param name="message"></param>
        public void SetError(string message)
        {
            if (message == null || message == "CLEAR")
            {
                ErrorMessage = string.Empty;
                return;
            }
            ErrorMessage += message;
        }

        ///// <summary>
        ///// Set the error from an exception object
        ///// </summary>
        ///// <param name="ex"></param>
        //public void SetError(Exception ex)
        //{
        //    SetError(ex, false);
        //}


        /// <summary>
        /// Set from exception and optionally use inner exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="checkInner"></param>
        public void SetError(Exception ex, bool checkInner = false)
        {
            if (ex == null)
                ErrorMessage = string.Empty;

            Exception e = ex;
            if (checkInner)
                e = e.GetBaseException();

            ErrorMessage = e.Message;
        }
        #endregion

        #region GenericPropertyStorage

        /// <summary>
        // Dictionary of arbitrary property values that can be attached
        // to the current object. You can use GetProperties, SetProperties
        // to load the properties to and from a text field.
        /// </summary>
        public PropertyBag Properties
        {
            get
            {
                if (_Properties == null)
                    _Properties = new PropertyBag();
                return _Properties;
            }
            private set { _Properties = value; }
        }
        private PropertyBag _Properties = null;

        /// <summary>
        /// Retrieves a value from the Properties collection safely.
        /// If the value doesn't exist null is returned.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object GetProperty(string key)
        {
            if (Properties == null)
                return null;

            object value;
            Properties.TryGetValue(key, out value);

            return value;
        }

        /// <summary>
        /// Loads the Properties dictionary with values from a Properties property of 
        /// an entity object. Once loaded you can access the dictionary to read and write
        /// values from it arbitrarily and use SetProperties to write the values back
        /// in serialized form to the underlying property for database storage.
        /// </summary>
        /// <param name="stringFieldNameToLoadFrom">The name of the field to load the XML properties from.</param>
        protected void GetProperties(string stringFieldNameToLoadFrom = "Properties", object entity = null)
        {
            Properties = null;

            if (entity == null)
                entity = this.Item;

            // Always create a new property bag
            Properties = new PropertyBag();

            string fieldValue = ReflectionUtils.GetProperty(entity, stringFieldNameToLoadFrom) as string;
            if (string.IsNullOrEmpty(fieldValue))
                return;

            // load up Properties from XML                       
            Properties.FromXml(fieldValue);
        }

        /// <summary>
        /// Saves the Properties Dictionary - in serialized string form - to a specified entity field which 
        /// in turn allows writing the data back to the database.
        /// </summary>
        /// <param name="stringFieldToSaveTo"></param>
        protected void SetProperties(string stringFieldToSaveTo = "Properties", object entity = null)
        {
            if (entity == null)
                entity = Item;

            string xml = null;
            if (Properties.Count > 0)
            {
                // Serialize to Xm
                xml = Properties.ToXml();
            }
            ReflectionUtils.SetProperty(Item, stringFieldToSaveTo, xml);
        }
        #endregion

        /// <summary>
        /// Clear data access component
        /// </summary>
        public void Dispose()
        {
        }
    }


    public enum QueueMessageStatus
    {
        None,
        Submitted,
        Completed,
        Canceled      
    }
}
