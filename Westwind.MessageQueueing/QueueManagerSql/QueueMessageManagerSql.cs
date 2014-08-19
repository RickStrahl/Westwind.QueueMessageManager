using System;
using System.Linq;
using Westwind.MessageQueueing.Properties;
using System.Collections.Generic;
using Westwind.Utilities.Data;

namespace Westwind.MessageQueueing
{
    /// <summary>
    /// An implementation of a SQL based multi-access Queue
    /// that provides random acccess to requests so they can be retrived
    /// for long running tasks where both client and server can interact
    /// with each message for processing.
    /// 
    /// This implementation uses purely SQL server data access to handle
    /// the queue which works well for low to medium work loads and as long
    /// as waiting queue items stay relatively low (under a few thousand).
    /// If load gets heavier than that use the MsMq variant that offloads
    /// the next message de-queueing to MsMq, while leaving the actual 
    /// messages in SQL Server.
    /// 
    /// Great for long running tasks or even light workflow scenarios.
    /// </summary>    
    public class QueueMessageManagerSql : QueueMessageManager, IDisposable
    {

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
            private set { _Db = value;  }
        }
        private SqlDataAccess _Db;

        /// <summary>
        /// If true automatically attempts to create the database if
        /// it doesn't exist. Note this adds a little overhead as a single
        /// query is run to check for existance. False by default.
        /// </summary>
        public bool AutoCreateDataStore { get; set;  }

        public QueueMessageManagerSql() : base()
        {
        }
    
        public QueueMessageManagerSql(string connectionString) : base(connectionString)
        {            
        }


        /// <summary>
        /// Loads a Queue Item by its associated id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override QueueMessageItem Load(string id)
        {
            Item = Db.Find<QueueMessageItem>("select * from QueueMessageItems where id=@1", 1, id);

            if (Item == null)
                SetError(Db.ErrorMessage);
            else
            {
                // load up Properties from XmlProperties field
                this.GetProperties("XmlProperties", Item);
                Item.__IsNew = false;
            }

            return Item;
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
            if (item == null)
                item = Item;

            // Write the Properties collection to the XmlProperties field
            SetProperties("XmlProperties", item);


            bool result = false;
            if (!item.__IsNew)
            {
                result = Db.UpdateEntity(item, "QueueMessageItems", "Id", "Id");
                if (!result)
                    SetError(Db.ErrorMessage);
            }
            else
            {
                Db.ErrorMessage = null;

                Db.InsertEntity(item, "QueueMessageItems");
                if (!string.IsNullOrEmpty(Db.ErrorMessage))
                    SetError(Db.ErrorMessage);
                else
                {
                    result = true;
                    item.__IsNew = false;
                    Item.__IsNew = false;
                }
            }

            return result;
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
            
            var enumItems = Db.ExecuteStoredProcedureReader<QueueMessageItem>("qmm_GetNextQueueMessageItem",
                                                                              Db.CreateParameter("@QueueName", queueName));
            if (enumItems == null)
            {
                SetError(Db.ErrorMessage);
                return null;
            }

            try
            {
                Item = enumItems.FirstOrDefault();
            }
            catch (Exception ex)
            {
                SetError(ex, true);
                return null;
            }

            if (Item == null)
                return null;

            Item.__IsNew = false;
            Item.Status = "Started";

            // load up Properties from XmlProperties field
            this.GetProperties("XmlProperties", Item);

            return Item;
        }


        /// <summary>
        /// Deletes a particular message by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override bool DeleteMessage(string id)
        {
            SetError();

            int result = Db.ExecuteNonQuery("Delete from queuemessageItems where id=@0", id);
            if (result == -1)
            {
                SetError(Db.ErrorMessage);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Deletes all pending messages
        /// </summary>
        /// <param name="queueName">Optional queue to delete them on. If null all are deleted</param>
        /// <returns></returns>
        public override bool DeleteWaitingMessages(string queueName = null)
        {
            string sql = "delete from QueueMessageItems where ISNULL(Started,0) = 0";
            if (queueName != null)
                sql += " and QueueName=@0";

            int res = Db.ExecuteNonQuery(sql,queueName);
            if (res < 0)
            {
                SetError(Db.ErrorMessage);
                return false;
            }

            return true;
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
        public override bool IsCompleted(string id = null)
        {
            if (string.IsNullOrEmpty(id))
                id = Item.Id;

            object res = Db.ExecuteScalar("select id from QueueMessageItems where id=@0 and completed is not null",id);
            if (res == null)
                return false;

            return true;
        }
        /// <summary>
        /// Returns a list of recent queue items
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="itemCount"></param>
        /// <returns></returns>
        public override IEnumerable<QueueMessageItem> GetRecentQueueItems(string queueName = null, int itemCount = 25)
        {
            string sql = "select top " + itemCount + " * from QueueMessageItems with (NOLOCK)";

            if (queueName != null)
                sql += " where QueueName=@0 ";

            sql += "order by submitted desc";
            
            var items = Db.Query<QueueMessageItem>(sql, queueName);
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
        public override IEnumerable<QueueMessageItem> GetPendingQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (maxCount == 0)
                maxCount = INT_maxCount;
            if (queueName == null)
                queueName = string.Empty;

            IEnumerable<QueueMessageItem> items;

            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                        "WHERE queueName=@0 AND iscomplete = 0 AND started is not null AND completed is null " +
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
        /// <returns>Count or -1 on failure</returns>
        public override int GetWaitingQueueMessageCount(string queueName = null)
        {            
            object result = Db.ExecuteScalar("select count(id) from QueueMessageItems with (NOLOCK) " +
                    "WHERE " + 
                    (queueName != null ? "queueName=@0 AND " : "" ) + 
                    "started is null", queueName);
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
        /// <returns>list of messages or null</returns>
        public override IEnumerable<QueueMessageItem> GetWaitingQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (maxCount == 0)
                maxCount = INT_maxCount;
            if (queueName == null)
                queueName = string.Empty;

            IEnumerable<QueueMessageItem> items;

            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems (NOLOCK) " +
                    "WHERE QueueName=@0 AND started is null " +
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
        public override IEnumerable<QueueMessageItem> GetCompleteQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (maxCount == 0)
                maxCount = INT_maxCount;
            if (queueName == null)
                queueName = string.Empty;

            IEnumerable<QueueMessageItem> items;

            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE queueName=@0 AND iscomplete = 1" +
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
        public override IEnumerable<QueueMessageItem> GetTimedOutQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (queueName == null)
                queueName = string.Empty;
            if (maxCount == 0)
                maxCount = INT_maxCount;

            DateTime dt = DateTime.UtcNow.Subtract(this.MessageTimeout);

            IEnumerable<QueueMessageItem> items;
            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE queueName=@0 AND iscomplete = 0 AND started < @1 " +
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
        public override IEnumerable<QueueMessageItem> GetCancelledMessages(string queueName = null, int maxCount = 0)
        {
            if (queueName == null)
                queueName = string.Empty;
            if (maxCount == 0)
                maxCount = INT_maxCount;
            
            IEnumerable<QueueMessageItem> items;
            items = Db.Query<QueueMessageItem>("select TOP " + maxCount + " * from QueueMessageItems " +
                    "WHERE queueName=@0 AND iscancelled = 1 " +
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
                connectionString = ConnectionString;            

            var db = new SqlDataAccess(connectionString);

            if (AutoCreateDataStore)
            {
                var result = db.ExecuteNonQuery("select id from QueueMessageItems where id='@!@'");
                if (result == -1)
                {

                    // table doesn't exist - try to create
                    if (db.ErrorNumber == -2146232060)
                    {
                        // hack - avoid recursion here because 
                        // _Db is not set yet when in constructor
                        _Db = db;
                        if (!CreateDatastore())
                            throw new ArgumentException(Resources.CouldntAccessQueueDatabase + "\r\n" + ErrorMessage);
                    }
                    else if (db.ErrorNumber != 0)
                        throw new ArgumentException(Resources.CouldntAccessQueueDatabase);
                }
            }

            return db;
        }


        /// <summary>
        /// Method used to clear out 'old' messages to keep database size down
        /// Removes messages that have been started but not completed in the
        /// specified timeout period.
        /// </summary>
        public override bool ClearMessages(TimeSpan? messageTimeout = null)
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
        public override bool CreateDatastore()
        {
            SetError();
            
            if (!Db.RunSqlScript(CREATE_SQL_OBJECTS, false, false))
            {
                SetError(Db.ErrorMessage);
                return false;
            }
            return true;
        }


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
	[QueueName] [nvarchar](40) NULL,
	[Status] [nvarchar](50) NULL,
	[Action] [nvarchar](80) NULL,
	[Submitted] [datetime] NOT NULL,
	[Started] [datetime] NULL,
	[Completed] [datetime] NULL,
	[IsComplete] [bit] NOT NULL,
	[IsCancelled] [bit] NOT NULL,
	[Expire] [int] NOT NULL,
	[Message] [nvarchar](max) NULL,
    [TextInput] [nvarchar](max) NULL,	
    [TextResult] [nvarchar](max) NULL,	
	[NumberResult] [decimal](18, 2) NOT NULL,	
    [Data] [nvarchar] (max) NULL,    
    [BinData] [varbinary](max) NULL,
    [Xml] [nvarchar](max) NULL,
    [Json] [nvarchar] (max) NULL,		
	[PercentComplete] [int] NOT NULL,
	[XmlProperties] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo.QueueMessageItems] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]


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
    
    CREATE NONCLUSTERED INDEX [IX_QueueMessageItems_StartedNull] ON [dbo].[QueueMessageItems]
    (
	    [Started] ASC
    )
    WHERE ([Started] IS NULL)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]        
    CREATE NONCLUSTERED INDEX [IX_QueueMessageItems_QueueName] ON [dbo].[QueueMessageItems]
    (
	    [QueueName] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]     
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[qmm_GetNextQueueMessageItem]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE  [dbo].[qmm_GetNextQueueMessageItem]
  @QueueName nvarchar(80), @Count int = 1
 AS
 
   UPDATE QueueMessageItems
          SET [Started] = GetUtcDate(), [Status] = ''Started''
		  OUTPUT INSERTED.*		  
          WHERE Id in (
			  SELECT TOP(@Count)
				   Id FROM QueueMessageItems WITH (UPDLOCK)	   
				   WHERE [QueueName] =  @QueueName AND
                         [Started] is null         						 
				   -- ORDER BY Submitted 
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

}
