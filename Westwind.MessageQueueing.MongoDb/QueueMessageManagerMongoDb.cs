using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Westwind.MessageQueueing.MongoDb;

/// <summary>
/// MongoDB implementation of QueueMessageManager.
/// Provides a distributed queue using MongoDB as the data store.
/// </summary>
public class QueueMessageManagerMongoDb : QueueMessageManager, IDisposable
{
    private MongoClient _mongoClient;
    private IMongoDatabase _mongoDb;
    private IMongoCollection<QueueMessageItem> _collection;
    private const string CollectionName = "QueueMessageItems";
    private const string DefaultDbName = "QueueMessageManager";
    private static readonly object _initLock = new object();

    /// <summary>
    /// Direct access to the MongoDB database for custom queries
    /// </summary>
    public IMongoDatabase Db
    {
        get
        {
            if (_mongoDb == null)
                InitializeDatabase();

            if (_mongoDb == null)
                throw new ArgumentException("Couldn't connect to MongoDB database");
            return _mongoDb;
        }
    }

    public QueueMessageManagerMongoDb() : base()
    {
    }

    public QueueMessageManagerMongoDb(string connectionString) : base(connectionString)
    {
    }

    /// <summary>
    /// Initialize MongoDB connection and ensure collection exists with proper indexes
    /// </summary>
    private void InitializeDatabase()
    {
        if (_mongoDb != null)
            return;

        lock (_initLock)
        {
            if (_mongoDb != null)
                return;

            try
            {
                var mongoUrl = MongoUrl.Create(ConnectionString ?? qmmApp.ConnectionString); //  "mongodb://localhost:27017"
                _mongoClient = new MongoClient(mongoUrl);
                
                var dbName = mongoUrl.DatabaseName ?? DefaultDbName;
                _mongoDb = _mongoClient.GetDatabase(dbName);

                if (AutoCreateTables)
                {
                    EnsureDataStoreExists();
                }
            }
            catch (Exception ex)
            {
                SetError(ex, true);
                throw;
            }
        }
    }

    /// <summary>
    /// Gets the collection, creating indexes if needed
    /// </summary>
    private IMongoCollection<QueueMessageItem> GetCollection()
    {
        if (_collection == null)
        {
            _collection = Db.GetCollection<QueueMessageItem>(CollectionName);
        }
        return _collection;
    }

    /// <summary>
    /// Loads a Queue Item by its ID
    /// </summary>
    public override QueueMessageItem Load(string id)
    {
        try
        {
            var collection = GetCollection();
            Item = collection.Find(x => x.Id == id).FirstOrDefault();

            if (Item == null)
            {
                SetError($"Item with ID '{id}' not found");
                return null;
            }

            Item.__IsNew = false;
            return Item;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Saves a queue item (insert or update)
    /// </summary>
    public override bool Save(QueueMessageItem item = null)
    {
        if (item == null)
            item = Item;

        if (item == null)
        {
            SetError("No item to save");
            return false;
        }

        try
        {
            var collection = GetCollection();

            if (item.__IsNew)
            {
                collection.InsertOne(item);
                item.__IsNew = false;
                return true;
            }
            else
            {
                var result = collection.ReplaceOne(
                    x => x.Id == item.Id,
                    item,
                    new ReplaceOptions { IsUpsert = false }
                );

                return result.ModifiedCount > 0 || result.MatchedCount > 0;
            }
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Gets the next waiting message from the queue and marks it as started
    /// </summary>
    public override QueueMessageItem GetNextQueueMessage(string queueName = null)
    {
        if (queueName == null)
            queueName = DefaultQueue;

        try
        {
            var collection = GetCollection();

            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName),
                Builders<QueueMessageItem>.Filter.Eq(x => x.Started, null)
            );

            var update = Builders<QueueMessageItem>.Update
                .Set(x => x.Started, DateTime.UtcNow)
                .Set(x => x.Status, "Started");

            var options = new FindOneAndUpdateOptions<QueueMessageItem>
            {
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<QueueMessageItem>.Sort.Ascending(x => x.Submitted)
            };

            Item = collection.FindOneAndUpdate(filter, update, options);

            if (Item == null)
                return null;

            Item.__IsNew = false;
            return Item;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Deletes all waiting messages in a queue
    /// </summary>
    public override bool DeleteWaitingMessages(string queueName = null)
    {
        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.Eq(x => x.Started, null);

            if (!string.IsNullOrEmpty(queueName))
            {
                filter = Builders<QueueMessageItem>.Filter.And(
                    filter,
                    Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName)
                );
            }

            var result = collection.DeleteMany(filter);
            return result.DeletedCount > 0 || result.IsAcknowledged;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Determines if a message is completed
    /// </summary>
    public override bool IsCompleted(string id = null)
    {
        if (string.IsNullOrEmpty(id))
            id = Item?.Id;

        if (string.IsNullOrEmpty(id))
        {
            SetError("No ID provided");
            return false;
        }

        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.Id, id),
                Builders<QueueMessageItem>.Filter.Ne(x => x.Completed, null)
            );

            return collection.Find(filter).Any();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Gets recent queue items
    /// </summary>
    public override IEnumerable<QueueMessageItem> GetRecentQueueItems(string queueName = null, int itemCount = 25)
    {
        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.Empty;

            if (!string.IsNullOrEmpty(queueName))
                filter = Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName);

            return collection.Find(filter)
                .Sort(Builders<QueueMessageItem>.Sort.Descending(x => x.Submitted))
                .Limit(itemCount)
                .ToList();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Gets pending queue messages (started but not completed)
    /// </summary>
    public override IEnumerable<QueueMessageItem> GetPendingQueueMessages(string queueName = null, int maxCount = 0)
    {
        if (maxCount == 0)
            maxCount = INT_maxCount;

        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName ?? string.Empty),
                Builders<QueueMessageItem>.Filter.Eq(x => x.IsComplete, false),
                Builders<QueueMessageItem>.Filter.Ne(x => x.Started, null),
                Builders<QueueMessageItem>.Filter.Eq(x => x.Completed, null)
            );

            return collection.Find(filter)
                .Sort(Builders<QueueMessageItem>.Sort.Descending(x => x.Started))
                .Limit(maxCount)
                .ToList();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Gets count of waiting queue messages
    /// </summary>
    public override int GetWaitingQueueMessageCount(string queueName = null)
    {
        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName ?? string.Empty),
                Builders<QueueMessageItem>.Filter.Eq(x => x.Started, null)
            );

            return (int)collection.CountDocuments(filter);
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return -1;
        }
    }

    /// <summary>
    /// Gets waiting queue messages
    /// </summary>
    public override IEnumerable<QueueMessageItem> GetWaitingQueueMessages(string queueName = null, int maxCount = 0)
    {
        if (maxCount == 0)
            maxCount = INT_maxCount;

        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName ?? string.Empty),
                Builders<QueueMessageItem>.Filter.Eq(x => x.Started, null)
            );

            return collection.Find(filter)
                .Sort(Builders<QueueMessageItem>.Sort.Descending(x => x.Submitted))
                .Limit(maxCount)
                .ToList();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Gets completed queue messages
    /// </summary>
    public override IEnumerable<QueueMessageItem> GetCompleteQueueMessages(string queueName = null, int maxCount = 0)
    {
        if (maxCount == 0)
            maxCount = INT_maxCount;

        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName ?? string.Empty),
                Builders<QueueMessageItem>.Filter.Eq(x => x.IsComplete, true)
            );

            return collection.Find(filter)
                .Sort(Builders<QueueMessageItem>.Sort.Descending(x => x.Completed))
                .Limit(maxCount)
                .ToList();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Gets timed out queue messages
    /// </summary>
    public override IEnumerable<QueueMessageItem> GetTimedOutQueueMessages(string queueName = null)
    {
        try
        {
            var collection = GetCollection();
            var timeoutThreshold = DateTime.UtcNow.Subtract(MessageTimeout);

            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName ?? string.Empty),
                Builders<QueueMessageItem>.Filter.Eq(x => x.IsComplete, false),
                Builders<QueueMessageItem>.Filter.Lt(x => x.Started, timeoutThreshold)
            );

            return collection.Find(filter)
                .Sort(Builders<QueueMessageItem>.Sort.Descending(x => x.Started))
                .ToList();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Updates timed out queue messages
    /// </summary>
    public override bool UpdateTimedOutQueueMessages(string queueName = null, TimeoutActions timeoutAction = TimeoutActions.Timeout)
    {
        try
        {
            var timedOutMessages = GetTimedOutQueueMessages(queueName);
            if (timedOutMessages == null)
                return false;

            foreach (var item in timedOutMessages)
            {
                if (timeoutAction == TimeoutActions.Timeout)
                {
                    FailRequest(item);
                    item.Status = "TimedOut";
                }
                else if (timeoutAction == TimeoutActions.Delete)
                {
                    DeleteMessage(item.Id);
                    continue;
                }
                else if (timeoutAction == TimeoutActions.Reset)
                {
                    ResubmitRequest(item);
                }
                else if (timeoutAction == TimeoutActions.Fail)
                {
                    item.Fail("Message timed out");
                }
                else if (timeoutAction == TimeoutActions.Cancel)
                {
                    item.Cancel();
                    item.Message = "Message timed out and cancelled";
                }

                Save(item);
            }

            return true;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Gets cancelled messages
    /// </summary>
    public override IEnumerable<QueueMessageItem> GetCancelledMessages(string queueName = null, int maxCount = 0)
    {
        if (maxCount == 0)
            maxCount = INT_maxCount;

        try
        {
            var collection = GetCollection();
            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Eq(x => x.QueueName, queueName ?? string.Empty),
                Builders<QueueMessageItem>.Filter.Eq(x => x.IsCancelled, true)
            );

            return collection.Find(filter)
                .Sort(Builders<QueueMessageItem>.Sort.Descending(x => x.Started))
                .Limit(maxCount)
                .ToList();
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return null;
        }
    }

    /// <summary>
    /// Clears timed out messages from the database
    /// </summary>
    public override bool ClearTimedoutMessages(TimeSpan? messageTimeout = null)
    {
        try
        {
            if (messageTimeout == null)
                messageTimeout = MessageTimeout;

            var collection = GetCollection();
            var cutoffDate = DateTime.UtcNow.Subtract(messageTimeout.Value);
            var oneMonthEarlier = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var filter = Builders<QueueMessageItem>.Filter.And(
                Builders<QueueMessageItem>.Filter.Lt(x => x.Started, cutoffDate),
                Builders<QueueMessageItem>.Filter.Gt(x => x.Started, oneMonthEarlier)
            );

            var result = collection.DeleteMany(filter);
            return result.IsAcknowledged;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Ensures the MongoDB collection exists and creates indexes
    /// </summary>
    public override bool EnsureDataStoreExists()
    {
        try
        {
            var collection = GetCollection();

            // Create indexes for common queries
            var indexKeysBuilder = Builders<QueueMessageItem>.IndexKeys;

            // Index for finding waiting messages by queue
            var waitingIndex = indexKeysBuilder.Ascending(x => x.QueueName)
                .Ascending(x => x.Started)
                .Ascending(x => x.Submitted);
            collection.Indexes.CreateOne(new CreateIndexModel<QueueMessageItem>(waitingIndex,
                new CreateIndexOptions { Name = "IX_QueueName_Started_Submitted" }));

            // Index for finding completed messages
            var completeIndex = indexKeysBuilder.Ascending(x => x.IsComplete)
                .Descending(x => x.Completed);
            collection.Indexes.CreateOne(new CreateIndexModel<QueueMessageItem>(completeIndex,
                new CreateIndexOptions { Name = "IX_IsComplete_Completed" }));

            // Index for finding started messages
            var startedIndex = indexKeysBuilder.Ascending(x => x.Started);
            collection.Indexes.CreateOne(new CreateIndexModel<QueueMessageItem>(startedIndex,
                new CreateIndexOptions { Name = "IX_Started" }));

            // Index for finding by status
            var statusIndex = indexKeysBuilder.Ascending(x => x.Status);
            collection.Indexes.CreateOne(new CreateIndexModel<QueueMessageItem>(statusIndex,
                new CreateIndexOptions { Name = "IX_Status" }));

            return true;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Deletes a message by ID
    /// </summary>
    public override bool DeleteMessage(string id)
    {
        try
        {
            var collection = GetCollection();
            var result = collection.DeleteOne(x => x.Id == id);
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// Dispose and clean up resources
    /// </summary>
    public override void Dispose()
    {
        _mongoClient = null;
        _mongoDb = null;
        _collection = null;

        base.Dispose();
    }
}