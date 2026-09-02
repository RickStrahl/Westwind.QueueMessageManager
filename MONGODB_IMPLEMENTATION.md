# QueueMessageManagerMongoDb Implementation

## Overview

The `QueueMessageManagerMongoDb` class provides a complete MongoDB implementation of the QueueMessageManager. It uses the latest MongoDB .NET driver (v2.27.0) for reliable, production-ready queue management.

## Key Features

### 1. **Direct Database Access via `Db` Property**

The implementation provides a `Db` property that gives direct access to the MongoDB database instance for custom queries:

```csharp
var queueManager = new QueueMessageManagerMongoDb("mongodb://localhost:27017/QueueMessageManager");

// Access the raw MongoDB database for custom operations
IMongoDatabase mongoDb = queueManager.Db;
var collection = mongoDb.GetCollection<QueueMessageItem>("QueueMessageItems");

// Execute custom queries
var customQuery = collection.Find(x => x.Status == "Custom").ToList();
```

### 2. **Complete Queue Management**

All abstract methods from `QueueMessageManager` are fully implemented:

#### Core Operations
- **Load(id)** - Load a message by ID
- **Save(item)** - Insert or update a message
- **GetNextQueueMessage(queueName)** - Atomically fetch and mark the next waiting message as started
- **DeleteMessage(id)** - Delete a specific message
- **DeleteWaitingMessages(queueName)** - Delete all waiting messages in a queue

#### Query Operations
- **GetRecentQueueItems(queueName, itemCount)** - Get most recent items
- **GetWaitingQueueMessages(queueName, maxCount)** - Get messages waiting to be processed
- **GetWaitingQueueMessageCount(queueName)** - Count of waiting messages
- **GetPendingQueueMessages(queueName, maxCount)** - Get started but incomplete messages
- **GetCompleteQueueMessages(queueName, maxCount)** - Get finished messages
- **GetCancelledMessages(queueName, maxCount)** - Get cancelled messages
- **GetTimedOutQueueMessages(queueName)** - Get messages that exceeded timeout

#### Status Operations
- **IsCompleted(id)** - Check if a message is completed
- **UpdateTimedOutQueueMessages(queueName, timeoutAction)** - Process timed out messages
- **ClearTimedoutMessages(messageTimeout)** - Remove old timed out messages

### 3. **Automatic Index Creation**

The implementation automatically creates the following MongoDB indexes for optimal query performance:

- **IX_QueueName_Started_Submitted** - For efficient waiting message queries
- **IX_IsComplete_Completed** - For completed message queries
- **IX_Started** - For timeout operations
- **IX_Status** - For status-based queries

### 4. **Connection String Support**

Supports standard MongoDB connection strings:

```csharp
// Local MongoDB
var manager = new QueueMessageManagerMongoDb("mongodb://localhost:27017/QueueMessageManager");

// Atlas Cloud MongoDB
var manager = new QueueMessageManagerMongoDb(
    "mongodb+srv://username:password@cluster.mongodb.net/QueueMessageManager?retryWrites=true&w=majority"
);

// With authentication
var manager = new QueueMessageManagerMongoDb(
    "mongodb://user:password@server:27017/QueueMessageManager"
);
```

## Usage Examples

### Basic Usage

```csharp
var manager = new QueueMessageManagerMongoDb("mongodb://localhost:27017/QueueMessageManager");
manager.DefaultQueue = "default";
manager.AutoCreateTables = true;

// Create and submit a message
var item = manager.CreateItem();
item.Message = "Process this task";
item.Action = "MyAction";
manager.Save(item);

// Get next message to process
var nextItem = manager.GetNextQueueMessage();
if (nextItem != null)
{
    // Process the message
    manager.ProgressRequest(nextItem, "Processing...", 50, autoSave: true);
    
    // Complete it
    manager.CompleteRequest(nextItem, "Done!", autoSave: true);
}
```

### Custom Queries via Db Property

```csharp
var manager = new QueueMessageManagerMongoDb("mongodb://localhost:27017");

// Get the database for custom queries
var db = manager.Db;
var collection = db.GetCollection<QueueMessageItem>("QueueMessageItems");

// Complex filter
var filter = Builders<QueueMessageItem>.Filter.And(
    Builders<QueueMessageItem>.Filter.Eq(x => x.Status, "Started"),
    Builders<QueueMessageItem>.Filter.Gt(x => x.PercentComplete, 50)
);

var highProgressItems = collection.Find(filter).ToList();

// Aggregation pipeline
var results = collection.Aggregate()
    .Match(x => x.QueueName == "MyQueue")
    .Group(x => x.Status, g => new { Status = g.Key, Count = g.Count() })
    .ToList();
```

### Timeout Handling

```csharp
var manager = new QueueMessageManagerMongoDb("mongodb://localhost:27017");
manager.MessageTimeout = TimeSpan.FromMinutes(30);

// Update timed out messages - fail them
manager.UpdateTimedOutQueueMessages("default", TimeoutActions.Fail);

// Or reset them for retry
manager.UpdateTimedOutQueueMessages("default", TimeoutActions.Reset);

// Or delete them
manager.UpdateTimedOutQueueMessages("default", TimeoutActions.Delete);

// Clean up old timed out messages
manager.ClearTimedoutMessages();
```

## Dependency Injection Setup

```csharp
// In your DI configuration
services.AddSingleton<QueueMessageManager>(provider => 
{
    var connectionString = configuration.GetConnectionString("MongoDB");
    return new QueueMessageManagerMongoDb(connectionString);
});

// Usage in your service
public class MyService
{
    private readonly QueueMessageManager _queue;
    
    public MyService(QueueMessageManager queue)
    {
        _queue = queue;
    }
    
    public void ProcessQueue()
    {
        var item = _queue.GetNextQueueMessage("myqueue");
        if (item != null)
        {
            // Process...
        }
    }
}
```

## Performance Considerations

1. **Connection Pooling** - The MongoDB driver automatically handles connection pooling
2. **Batch Operations** - Use the `Db` property for batch updates/deletes for better performance
3. **Index Usage** - Queries are optimized using the automatically created indexes
4. **Queue Size** - Works efficiently with thousands of messages; consider archiving old completed messages

## Configuration

The implementation respects the following `QueueMessageManager` base properties:

- **ConnectionString** - MongoDB connection string
- **DefaultQueue** - Default queue name if not specified
- **MessageTimeout** - TimeSpan for message timeout (default: 2 hours)
- **AutoCreateTables** - Automatically create collection and indexes (default: false)

## Error Handling

All operations set the `ErrorMessage` property on failure:

```csharp
var manager = new QueueMessageManagerMongoDb(connectionString);
if (!manager.Save(item))
{
    Console.WriteLine($"Save failed: {manager.ErrorMessage}");
}
```

## Thread Safety

The implementation is thread-safe for:
- Initial database connection
- Collection access and queries
- Insert/update/delete operations

MongoDB handles concurrent access safely through its driver.

## Disposal

Always dispose of the manager when done:

```csharp
using (var manager = new QueueMessageManagerMongoDb(connectionString))
{
    // Use the manager
}
// Automatically disposed
```
