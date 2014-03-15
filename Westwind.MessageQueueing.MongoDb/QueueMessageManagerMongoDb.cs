using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;

namespace Westwind.MessageQueueing
{
    /// <summary>
    /// An implementation of a MongoDb based multi-access Queue
    /// that provides random acccess to requests so they can be retrived
    /// for long running tasks where both client and server can interact
    /// with each message for processing.
    /// 
    /// This implementation uses purely MongoDb server data access to handle
    /// the queue which works well for low to high volume loads.
    /// 
    /// Great for long running tasks or even light workflow scenarios.
    /// </summary>    
    public class QueueMessageManagerMongoDb : QueueMessageManager
    {

        public MongoDatabase Db
        {
            get
            {
                if (_Db == null)
                    _Db = this.GetDatabase(ConnectionString);

                if (_Db == null)
                    throw new ArgumentException("Couldn't connect to database.");
                return _Db;
            }
            private set { _Db = value;  }
        }
        private MongoDatabase _Db;

        public MongoCollection<QueueMessageItem> Collection
        {
            get
            {
                if (_collection != null)
                    return _collection;

                if (!Db.CollectionExists("QueueMessageItems"))
                    Db.CreateCollection("QueueMessageItems");

                _collection = Db.GetCollection<QueueMessageItem>("QueueMessageItems");
                return _collection;
            }
        }
        private MongoCollection<QueueMessageItem> _collection;    


        public QueueMessageManagerMongoDb() : base()
        {}

        public QueueMessageManagerMongoDb(string connectionString) : base(connectionString)
        {}

        /// <summary>
        /// Loads a Queue Message Item by its ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override QueueMessageItem Load(string id)
        {
            var query =  Query.EQ( "_id",id);
            Item = Collection.FindOne(query);            
            return Item;            
        }


        /// <summary>
        /// Deletes an individual queue message by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override bool DeleteMessage(string id)
        {
            SetError();

            var query = Query.EQ("_id", id);
            var result = Collection.Remove(query);
            if (!result.Ok)
            {
                SetError(result.ErrorMessage);
                return false;
            }

            return true;
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
            SetError();

            if (item == null)
                item = Item;
            if (item == null)
            {
                SetError("No item passed to save.");
                return false;
            }

            var result = Collection.Save(item);
            if (!result.HasLastErrorMessage)
                return true;

            SetError(result.ErrorMessage);
            return false;
        }
     
        /// <summary>
        /// Retrieves the next waiting Message from the Queue based 
        /// on a provided queueName
        /// </summary>
        /// <param name="queueName">Name of the queue</param>
        /// <returns>
        /// item or null. 
        /// Null can be returned when there are no items 
        /// or when there is an error. To check for error check 
        /// </returns>     
        public override QueueMessageItem GetNextQueueMessage(string queueName = null)
        {
            SetError();

            List<IMongoQuery> queries = new List<IMongoQuery>();

            if (queueName != null)
                queries.Add(Query.EQ("QueueName", queueName));

            queries.Add(Query.EQ("Started", BsonNull.Value));

            var query = Query.And(queries);
            var sort = SortBy.Ascending("Submitted");
            var update = Update.Set("Started", DateTime.UtcNow).Set("Status","Started");

            var result = Collection.FindAndModify(query, sort, update, true);
             
            if (!result.Ok)
            {
                SetError(result.ErrorMessage);
                return null;
            }

            Item = result.GetModifiedDocumentAs<QueueMessageItem>();
            return Item;
        }


        /// <summary>
        /// Deletes all messages that are waiting to be processed
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public override bool DeleteWaitingMessages(string queueName = null)
        {
            SetError();

            List<IMongoQuery> queries = new List<IMongoQuery>();

            if (queueName != null)
                queries.Add(Query.EQ("QueueName", queueName));

            queries.Add(Query.EQ("Started", BsonNull.Value));
            var query = Query.And(queries);
                        
            var result = Collection.Remove(query);

            if (!result.Ok)
            {
                SetError(result.ErrorMessage);
                return false;
            }
            return true;
        }




        /// <summary>
        /// Determines if anqueue has been completed
        /// successfully or failed.
        /// 
        /// Note this method returns true if the request
        /// has completed or cancelled/failed. It just
        /// checks for completion.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override bool IsCompleted(string id = null)
        {            
            if (string.IsNullOrEmpty(id))
                id = Item.Id;
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Invalid Id passed.");

            return Collection.AsQueryable()
                             .Any(qi => qi.Id == id && qi.Completed != null);            
        }

        /// <summary>
        /// Returns a list of recent queue items
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="itemCount">Max number of items to return</param>
        /// <returns></returns>
        public override IEnumerable<QueueMessageItem> GetRecentQueueItems(string queueName = null, int itemCount = 25)
        {
            SetError();

            if (queueName == null)
                queueName = string.Empty;

            return Collection.AsQueryable()
                .Where( qi=> qi.QueueName == queueName)
                .OrderByDescending(qi => qi.Submitted)
                .Take(itemCount);
        }

        /// <summary>
        /// Retrieves all messages that are pending, that have started
        /// but not completed yet. 
        /// </summary>
        /// <param name="queueName">Name of the queue to return items for</param>
        /// <param name="maxCount">Optional - max number of items to return</param>
        /// <returns>list of messages or null</returns>
        public override IEnumerable<QueueMessageItem> GetPendingQueueMessages(string queueName = null, int maxCount = 0)
        {
            SetError();

            if (queueName == null)
                queueName = string.Empty;

            var items = Collection.AsQueryable()
                .Where(qi => qi.QueueName == queueName && qi.Started != null && !qi.IsComplete);

            if (maxCount > 0)
                items = items.Take(maxCount);

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
            SetError();

            if (queueName == null)
                queueName = string.Empty;

            return Collection.AsQueryable()
                             .Count(qi => qi.QueueName == queueName && qi.Started == null);
        }

        /// <summary>
        /// Returns a count of messages that are waiting
        /// to be processed - this is the queue backup.
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns>list of messages or null</returns>
        public override IEnumerable<QueueMessageItem> GetWaitingQueueMessages(string queueName = null, int maxCount = 0)
        {
            if (queueName == null)
                queueName = string.Empty;

            return Collection.AsQueryable()
                .Where(qi => qi.QueueName == queueName && qi.Started == null )
                .OrderByDescending(qi => qi.Submitted);
        }

        public override IEnumerable<QueueMessageItem> GetCompleteQueueMessages(string queueName = null, int maxCount = 0)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<QueueMessageItem> GetTimedOutQueueMessages(string queueName = null, int maxCount = 0)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<QueueMessageItem> GetCancelledMessages(string queueName = null, int maxCount = 0)
        {
            throw new NotImplementedException();
        }

        public override bool ClearMessages(TimeSpan? messageTimeout = null)
        {
            throw new NotImplementedException();
        }

        public override bool CreateDatastore()
        {
            throw new NotImplementedException();
        }


 /// <summary>
        /// Creates a connection to a databaseName based on the Databasename and 
        /// optional server connection string.
        /// 
        /// Returned Mongo DatabaseName 'connection' can be cached and reused.
        /// </summary>
        /// <param name="connectionString">Mongo server connection string.
        /// Can either be a connection string entry name from the ConnectionStrings
        /// section in the config file or a full server string.        
        /// If not specified looks for connectionstring entry in  same name as
        /// the context. Failing that mongodb://localhost is used.
        ///  
        /// Examples:
        /// MyDatabaseConnectionString   (ConnectionStrings Config Name)       
        /// mongodb://localhost
        /// mongodb://localhost:22011/MyDatabase
        /// mongodb://username:password@localhost:22011/MyDatabase        
        /// </param>        
        /// <param name="databaseName">Name of the databaseName to work with if not specified on the connection string</param>
        /// <returns>Database instance</returns>
        protected virtual MongoDatabase GetDatabase(string connectionString = null,string databaseName= null)
        {
            // apply global values from this context if not passed
            if (string.IsNullOrEmpty(connectionString))
                connectionString = ConnectionString;

            // if not specified use connection string with name of queueName
            if (string.IsNullOrEmpty(connectionString))
                connectionString = this.GetType().Name;

            // is it a connection string name?
            if (!connectionString.Contains("://"))
            {
                var conn = ConfigurationManager.ConnectionStrings[connectionString];
                if (conn != null)
                    connectionString = conn.ConnectionString;
                else
                    connectionString = "mongodb://localhost";                
            }

            ConnectionString = connectionString;                

            var client = new MongoClient(connectionString);
            var server = client.GetServer();

            // is it provided on the connection string?
            if (string.IsNullOrEmpty(databaseName))
            {
                var uri = new Uri(connectionString);
                var path = uri.LocalPath;
                databaseName = uri.LocalPath.Replace("/", "");                
            }

            var db = server.GetDatabase(databaseName);            

            return db;
        }

        /// <summary>
        /// Clear data access component
        /// </summary>
        public void Dispose()
        {            
            _Db = null;
            _collection = null;
        }
    }
}
