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
    public class QueueMessageManager : IDisposable
    {
        private const int INT_maxCount = 99999;
        internal bool _IsNew = false;

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
        public QueueMessageItem Entity { get; set; }

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
        /// Data Access component to SQL Server database
        /// Configured via configuration settings or explicit
        /// QueueManagerConfiguration object passed in
        /// </summary>
        public SqlDataAccess Db
        {
            get
            {
                if (_Db == null)
                    _Db = LoadDal();

                if (_Db == null)
                    throw new ArgumentException(Resources.CouldntConnectToDatabase);
                return _Db;
            }
            private set { }
        }
        private SqlDataAccess _Db;

        /// <summary>
        /// Connection string used for this component
        /// </summary>
        public string ConnectionString { get; set; }


        public QueueMessageManager(QueueMessageManagerConfiguration configuration = null)
        {
            DefaultQueue = string.Empty;

            // 2 hours
            MessageTimeout = new TimeSpan(2, 0, 0);

            Serialization = new QueueMessageManagerSerializationHelper(this);

            if (configuration == null)
                Configuration = QueueMessageManagerConfiguration.Current;
            else
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
        public QueueMessageItem Load(string id)
        {
            Entity = Db.Find<QueueMessageItem>("select * from QueueMessageItems where id=@1", 1, id);

            if (Entity == null)
                SetError(Db.ErrorMessage);
            else
                // load up Properties from XmlProperties field
                this.GetProperties("XmlProperties", Entity);
            
            Entity.__IsNew = false;

            return Entity;
        }

        /// <summary>
        /// Retrieves the next pending Message from the Queue based on a provided type
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns>item or null. Null can be returned when there are no items or when there is an error</returns>
        public QueueMessageItem GetNextQueueMessage(string queueName = null)
        {
            if (queueName == null)
                queueName = DefaultQueue;

            var db = LoadDal();
            var enumItems = db.ExecuteStoredProcedureReader<QueueMessageItem>("qmm_GetNextQueueMessageItem",
                                                                              db.CreateParameter("@type", queueName));
            if (enumItems == null)
            {
                SetError(db.ErrorMessage);
                return null;
            }

            try
            {
                Entity = enumItems.FirstOrDefault();
            }
            catch (Exception ex)
            {
                SetError(ex, true);
                return null;
            }

            if (Entity == null)
                return null;

            Entity.__IsNew = false;
            Entity.Status = "Started";

            // load up Properties from XmlProperties field
            this.GetProperties("XmlProperties", Entity);

            return Entity;
        }

        /// <summary>
        /// Creates a new entity instance and properly
        /// initializes the instance's values.
        /// </summary>
        /// <returns></returns>
        public QueueMessageItem NewEntity(QueueMessageItem entity = null)
        {
            if (entity == null)
                Entity = new QueueMessageItem();
            else
                Entity = entity;

            Entity.__IsNew = true;

            // ensure entity is properly configured
            Entity.Submitted = DateTime.UtcNow;
            Entity.Status = "Submitted";
            Entity.Started = null;
            Entity.Completed = null;

            return Entity;
        }


        /// <summary>
        /// Saves the passed entity or the attached entity
        /// to the database. Call this after updating properties
        /// or individual values.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool Save(QueueMessageItem entity = null)
        {
            if (entity == null)
                entity = Entity;

            // Write the Properties collection to the XmlProperties field
            this.SetProperties("XmlProperties", entity);


            bool result = false;
            if (!entity.__IsNew)
            {
                result = Db.UpdateEntity(entity, "QueueMessageItems", "Id", "Id");
                if (!result)
                    SetError(Db.ErrorMessage);
            }
            else
            {
                Db.ErrorMessage = null;

                Db.InsertEntity(entity, "QueueMessageItems");
                if (!string.IsNullOrEmpty(Db.ErrorMessage))
                    SetError(Db.ErrorMessage);
                else
                {
                    result = true;
                    entity.__IsNew = false;
                    Entity.__IsNew = false;
                }
            }

            return result;
        }

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
        public bool IsCompleted(string id = null)
        {
            if (string.IsNullOrEmpty(id))
                id = Entity.Id;

            object res = Db.ExecuteScalar("select id from QueueMessageItems where id=@0 and completed is not null",id);
            if (res == null)
                return false;

            return true;
        }

        /// <summary>
        /// Sets the message properties for starting a new message request operation.
        /// Note the record is not written to the database use Save explicitly
        /// </summary>
        /// <param name="entity">An existing entity instance</param>
        public bool SubmitRequest(QueueMessageItem entity = null, string messageText = null, bool autoSave = false)
        {
            if (entity == null)
                entity = NewEntity();

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

            Entity = entity;

            if (autoSave)
                return Save();

            return true;
        }


        /// <summary>
        /// Sets the Entity record with the required settings
        /// to complete a request. Note record is not written
        /// to database - Call Save explicitly.
        /// </summary>
        public bool CompleteRequest(QueueMessageItem entity = null, string messageText = null, bool autoSave = false)
        {
            if (entity == null)
                entity = Entity;
            if (entity == null)
                entity = NewEntity();

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
        /// Sets the Entity record with the required settings
        /// to complete and cancel a request. Not saved to database
        /// call Save() explicitly.
        /// </summary>
        public bool CancelRequest(QueueMessageItem entity = null, string messageText = null, bool autoSave = false)
        {
            if (entity == null)
                entity = Entity;
            if (entity == null)
                entity = NewEntity();

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
                entity = Entity;

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
        public IEnumerable<QueueMessageItem> GetRecentQueueItems(string type = null, int itemCount = 25)
        {
            if (type == null)
                type = string.Empty;

            string sql = "select top " + itemCount + " * from QueueMessageItems where type=@0 order by submitted desc";
            
            var items = Db.Query<QueueMessageItem>(sql, type);
            if (items == null)
            {
                SetError(Db.ErrorMessage);
                return null;
            }

            return items;
        }


        /// <summary>
        /// Retrieves all messages that are pending, that have started
        /// but not completed yet. 
        /// </summary>
        /// <param name="queueName">Name of the queue to return items for</param>
        /// <param name="maxCount">Optional - max number of items to return</param>
        /// <returns></returns>
        public IEnumerable<QueueMessageItem> GetPendingQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (maxCount == 0)
                maxCount = INT_maxCount;
            if (queueName == null)
                queueName = string.Empty;

            IEnumerable<QueueMessageItem> items;

            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                        "WHERE type=@0 AND iscomplete = 0 AND started is not null AND completed is null " +
                        "ORDER BY started DESC", queueName);

            if (items == null)
                SetError(Db.ErrorMessage);

            return items;
        }

        /// <summary>
        /// Returns a count of messages that are waiting
        /// to be processed - this is the queue backup.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public int GetWaitingQueueMessageCount(string queueName = null)
        {
            if (queueName == null)
                queueName = string.Empty;

            object result = Db.ExecuteScalar("select count(id) from QueueMessageItems " +
                    "WHERE type=@0 AND started is null", queueName);
            if (result == null)
            {
                SetError(Db.ErrorMessage);
                return -1;
            }

            return (int)result;
        }


        /// <summary>
        /// Returns a count of messages that are waiting
        /// to be processed - this is the queue backup.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public IEnumerable<QueueMessageItem> GetWaitingQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (maxCount == 0)
                maxCount = INT_maxCount;
            if (queueName == null)
                queueName = string.Empty;

            IEnumerable<QueueMessageItem> items;

            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE type=@0 AND iscomplete = 0 and started is null " +
                    "ORDER BY submitted DESC", queueName);
            if (items == null)
                SetError(Db.ErrorMessage);

            return items;            
        }

        /// <summary>
        /// Result Cursor: TCompleteMessages
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public IEnumerable<QueueMessageItem> GetCompleteQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (maxCount == 0)
                maxCount = INT_maxCount;
            if (queueName == null)
                queueName = string.Empty;

            IEnumerable<QueueMessageItem> items;

            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE type=@0 AND iscomplete = 1" +
                    "ORDER BY completed DESC", queueName);
            if (items == null)
                SetError(Db.ErrorMessage);

            return items;

        }

        /// <summary>
        /// Returns a list of queue items that have timed out during processing.
        /// Not completed where started time is greater than the MessageTimeout.
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public IEnumerable<QueueMessageItem> GetTimedOutQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (queueName == null)
                queueName = string.Empty;
            if (maxCount == 0)
                maxCount = INT_maxCount;

            DateTime dt = DateTime.UtcNow.Subtract(this.MessageTimeout);

            IEnumerable<QueueMessageItem> items;
            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE type=@0 AND iscomplete = 0 AND started < @1 " +
                    "ORDER BY started DESC", queueName, dt);
            if (items == null)
                SetError(Db.ErrorMessage);

            return items;

        }

        /// <summary>
        /// Returns all messages in a queue that are cancelled
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public IEnumerable<QueueMessageItem> GetCancelledMessages(string queueName = null, int maxCount = 0)
        {
            if (queueName == null)
                queueName = string.Empty;
            if (maxCount == 0)
                maxCount = INT_maxCount;
            
            IEnumerable<QueueMessageItem> items;
            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE type=@0 AND iscancelled = 1 " +
                    "ORDER BY started DESC", queueName);
            
            if (items == null)
                SetError(Db.ErrorMessage);

            return items;
        }


        /// <summary>
        /// Generic routine to load up the data access layer.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        protected virtual SqlDataAccess LoadDal(string connectionString = null)
        {
            if (connectionString == null)
                connectionString = Configuration.ConnectionString;            

            var db = new SqlDataAccess(connectionString);

            var result = db.ExecuteNonQuery("select id from QueueMessageItems");
            
            //// table doesn't exist - try to create
            //if (db.ErrorNumber == -2146232060)
            //{
            //    // hack - avoid recursion here because 
            //    // _Db is not set yet when in constructor
            //    _Db = db; 
            //    if (!CreateDatabaseTable())
            //        throw new ArgumentException(Resources.CouldntAccessQueueDatabase);
            //}
            //else if (db.ErrorNumber != 0)
            //    throw new ArgumentException(Resources.CouldntAccessQueueDatabase);

            return db;
        }


        /// <summary>
        /// Method used to clear out 'old' messages to keep database size down
        /// Removes messages that have been started but not completed in the
        /// specified timeout period.
        /// </summary>
        public bool ClearMessages(TimeSpan? messageTimeout = null)
        {
            if (messageTimeout == null)
                messageTimeout = MessageTimeout;

            int result = Db.ExecuteNonQuery("delete from QueueMessageItems where Started<@0 and Started > @1", 
                                            DateTime.UtcNow.Subtract(messageTimeout.Value), 
                                            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            if (result == -1)
            {
                SetError(Db.ErrorMessage);
                return false;
            }
            
            return true;
        }


        /// <summary>
        /// Creates the DatabaseTable and stored procedure for the queue. Note this routine
        /// requires that a database exists already and uses the same connection string
        /// that is used for the main application.
        /// </summary>
        /// <returns></returns>
        public bool CreateDatabaseTable()
        {
            SetError();
            
            if (!Db.RunSqlScript(CREATE_SQL_OBJECTS, false, false))
            {
                SetError(Db.ErrorMessage);
                return false;
            }
            return true;
        }

        #region ErrorHandling


        public void SetError()
        {
            this.SetError("CLEAR");
        }

        public void SetError(string message)
        {
            if (message == null || message == "CLEAR")
            {
                this.ErrorMessage = string.Empty;
                return;
            }
            this.ErrorMessage += message;
        }

        public void SetError(Exception ex)
        {
            SetError(ex, false);
        }

        public void SetError(Exception ex, bool checkInner = false)
        {
            if (ex == null)
                this.ErrorMessage = string.Empty;

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
            if (this.Properties == null)
                return null;

            object value = null;
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
                entity = this.Entity;

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
                entity = this.Entity;

            //string xml = DataContractSerializationUtils.SerializeToXmlString(Properties,true);

            string xml = null;
            if (Properties.Count > 0)
            {
                // Serialize to Xm
                xml = Properties.ToXml();
            }
            ReflectionUtils.SetProperty(Entity, stringFieldToSaveTo, xml);
        }
        #endregion


        private const string CREATE_SQL_OBJECTS =
@"/****** Object:  StoredProcedure [dbo].[qmm_GetNextQueueMessageItem]    Script Date: 3/12/2013 12:29:36 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
/****** Object:  Table [dbo].[QueueMessageItems]    Script Date: 2/18/2013 12:29:36 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[QueueMessageItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[QueueMessageItems](
	    [Id] [nvarchar](50) NOT NULL,
	    [Type] [nvarchar](40) NULL,
	    [Status] [nvarchar](50) NULL,
	    [Action] [nvarchar](80) NULL,
	    [Submitted] [datetime] NOT NULL,
	    [Started] [datetime] NULL,
	    [Completed] [datetime] NULL,
	    [IsComplete] [bit] NOT NULL,
	    [IsCancelled] [bit] NOT NULL,
	    [Expire] [int] NOT NULL,
	    [Message] [nvarchar](max) NULL,
	    [TextResult] [nvarchar](max) NULL,
	    [BinResult] [varbinary](max) NULL,
	    [NumberResult] [decimal](18, 2) NOT NULL,
	    [Xml] [nvarchar](max) NULL,
	    [TextInput] [nvarchar](max) NULL,
	    [TextOutput] [nvarchar](max) NULL,
	    [PercentComplete] [int] NOT NULL,
	    [XmlProperties] [nvarchar](max) NULL,
     CONSTRAINT [PK_dbo.QueueMessageItems] PRIMARY KEY CLUSTERED 
    (
	    [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
 
    SET ANSI_PADDING OFF

    ALTER TABLE [dbo].[QueueMessageItems] ADD  CONSTRAINT [DF_QueueMessageItems_Id]  DEFAULT (CONVERT([nvarchar](36),newid())) FOR [Id]
    ALTER TABLE [dbo].[QueueMessageItems] ADD  CONSTRAINT [DF_QueueMessageItems_IsComplete]  DEFAULT ((0)) FOR [IsComplete]
    ALTER TABLE [dbo].[QueueMessageItems] ADD  CONSTRAINT [DF_QueueMessageItems_IsCancelled]  DEFAULT ((0)) FOR [IsCancelled]
    ALTER TABLE [dbo].[QueueMessageItems] ADD  CONSTRAINT [DF_QueueMessageItems_Expire]  DEFAULT ((0)) FOR [Expire]
    ALTER TABLE [dbo].[QueueMessageItems] ADD  CONSTRAINT [DF_QueueMessageItems_NumberResult]  DEFAULT ((0)) FOR [NumberResult]
    ALTER TABLE [dbo].[QueueMessageItems] ADD  CONSTRAINT [DF_QueueMessageItems_PercentComplete]  DEFAULT ((0)) FOR [PercentComplete]

    CREATE NONCLUSTERED INDEX [IX_QueueMessageItems_IsComplete] ON [dbo].[QueueMessageItems]
    (
	    [IsComplete] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]    
    CREATE NONCLUSTERED INDEX [IX_QueueMessageItems_Started] ON [dbo].[QueueMessageItems]
    (
	    [Started] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY] 
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[qmm_GetNextQueueMessageItem]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE  [dbo].[qmm_GetNextQueueMessageItem]
  @Type nvarchar(80)
 AS
 
   UPDATE QueueMessageItems
          SET [Started] = GetUtcDate(), [Status] = ''Started''
		  OUTPUT INSERTED.*		  
          WHERE Id in (
			  SELECT TOP 1 
				   Id FROM QueueMessageItems WITH (UPDLOCK)	   
				   WHERE  [Started] is null and              
						 type =  @Type
				   ORDER BY Submitted  
		  )
' 
END
GO

SET ANSI_PADDING OFF
GO
";

        /// <summary>
        /// Clear data access component
        /// </summary>
        public void Dispose()
        {
            if (Db != null)
            {
                Db.Dispose();
                Db = null;
            }
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
