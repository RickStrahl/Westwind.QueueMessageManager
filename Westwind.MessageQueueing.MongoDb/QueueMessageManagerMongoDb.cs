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
    public class QueueMessageManagerMongoDb : QueueMessageManager
    {
                /// <summary>
        /// Data Access component to SQL Server database
        /// Configured via configuration settings or explicit
        /// QueueManagerConfiguration object passed in
        /// </summary>
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

        /// <summary>
        /// Connection string used for this component
        /// </summary>
        public string ConnectionString { get; set; }


        public QueueMessageManagerMongoDb()
        {
            DefaultQueue = string.Empty;

            // 2 hours
            MessageTimeout = new TimeSpan(2, 0, 0);            
        }

        public QueueMessageManagerMongoDb(QueueMessageManagerConfiguration configuration)
        {
            DefaultQueue = string.Empty;

            // 2 hours
            MessageTimeout = new TimeSpan(2, 0, 0);

            Configuration = configuration;
            ConnectionString = Configuration.ConnectionString;
        }

        public QueueMessageManagerMongoDb(string connectionString) : this()
        {            
            ConnectionString = connectionString;
        }


        public override QueueMessageItem Load(string id)
        {
            var query = Query.EQ("Id", id);
            Item = Collection.FindOne(query);            
            return Item;            
        }

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

        public override QueueMessageItem GetNextQueueMessage(string queueName = null)
        {
            SetError();

            List<IMongoQuery> queries = new List<IMongoQuery>();

            if (queueName != null)
                queries.Add(Query.EQ("Type", queueName));

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

        public override bool DeletePendingMessages(string queueName = null)
        {
            SetError();

            List<IMongoQuery> queries = new List<IMongoQuery>();

            if (queueName != null)
                queries.Add(Query.EQ("Type", queueName));

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
        /// Check to see in the db if the item is completed
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


        public override IEnumerable<QueueMessageItem> GetRecentQueueItems(string type = null, int itemCount = 25)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<QueueMessageItem> GetPendingQueueMessages(string queueName = null, int maxCount = 0)
        {
            throw new NotImplementedException();
        }

        public override int GetWaitingQueueMessageCount(string queueName = null)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<QueueMessageItem> GetWaitingQueueMessages(string queueName = null, int maxCount = 0)
        {
            throw new NotImplementedException();
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
                connectionString = Configuration.ConnectionString;

            // if not specified use connection string with name of type
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
