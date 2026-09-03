using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using Westwind.MessageQueueing.MongoDb;

namespace Westwind.MessageQueueing.Tests;

[TestClass]
public class BasicQueueMessageManagerMongoDbTests
{
    private const string TestQueueName = "QmmMongoTestQueue";
    private const string TestConnectionString = "mongodb://localhost:27017/QueueMessageManagerTests";

    [TestInitialize]
    public void Initialize()
    {
        //if (!CanRunMongoTests())
        //    Assert.Inconclusive("MongoDB server is not compatible with MongoDB.Driver (requires MongoDB 3.6+).");
    }

    [TestMethod]
    public void ConstructorOverrideTest()
    {
        using var manager = new QueueMessageManagerMongoDb(TestConnectionString);
        Console.WriteLine(manager.ErrorMessage);
        Assert.AreEqual(TestConnectionString, manager.ConnectionString);
    }

    [TestMethod]
    public void SubmitAndLoadRequestTest()
    {
        using var manager = CreateManager();
        CleanupQueue(manager);

        var item = new QueueMessageItem
        {
            QueueName = TestQueueName,
            Message = "Mongo submit/load test",
            Action = "TEST"
        };

        manager.SubmitRequest(item);
        Assert.IsTrue(manager.Save(item), manager.ErrorMessage);

        var loaded = manager.Load(item.Id);
        Assert.IsNotNull(loaded, manager.ErrorMessage);
        Assert.AreEqual(item.Id, loaded.Id);
        Assert.AreEqual(TestQueueName, loaded.QueueName);
        Assert.AreEqual("Submitted", loaded.Status);
    }



    [TestMethod]
    public void GetNextQueueMessageItemWithAddedItemTest()
    {
        using var manager = CreateManager();
        CleanupQueue(manager);

        var item = new QueueMessageItem
        {
            QueueName = TestQueueName,
            Message = "Next queue message test",
            Action = "TEST"
        };

        manager.SubmitRequest(item);
        Assert.IsTrue(manager.Save(item), manager.ErrorMessage);

        var next = manager.GetNextQueueMessage(TestQueueName);
        Assert.IsNotNull(next, manager.ErrorMessage);
        Assert.AreEqual(item.Id, next.Id);
        Assert.AreEqual("Started", next.Status);
        Assert.IsNotNull(next.Started);
    }

    [TestMethod]
    public void CompleteMessageRoundTripTest()
    {
        using var manager = CreateManager();
        CleanupQueue(manager);

        var item = new QueueMessageItem
        {
            QueueName = TestQueueName,
            Message = "Complete flow test",
            Action = "TEST"
        };
        manager.SubmitRequest(item);
        Assert.IsTrue(manager.Save(item), manager.ErrorMessage);

        var processing = manager.GetNextQueueMessage(TestQueueName);
        Assert.IsNotNull(processing, manager.ErrorMessage);

        manager.CompleteRequest(processing, "Completed");
        Assert.IsTrue(manager.Save(processing), manager.ErrorMessage);

        var reloaded = manager.Load(processing.Id);
        Assert.IsNotNull(reloaded, manager.ErrorMessage);
        Assert.IsTrue(reloaded.IsComplete);
        Assert.AreEqual("Completed", reloaded.Status);
    }

    [TestMethod]
    public void GetWaitingMessagesCountTest()
    {
        using var manager = CreateManager();
        CleanupQueue(manager);

        for (var i = 0; i < 3; i++)
        {
            var item = new QueueMessageItem
            {
                QueueName = TestQueueName,
                Message = $"Waiting item {i}",
                Action = "TEST"
            };
            manager.SubmitRequest(item);
            Assert.IsTrue(manager.Save(item), manager.ErrorMessage);
        }

        var count = manager.GetWaitingQueueMessageCount(TestQueueName);
        Assert.AreEqual(3, count);
        
    }

    [TestMethod]
    public void GetRecentMessagesTest()
    {
        using var manager = CreateManager();
        CleanupQueue(manager);

        for (var i = 0; i < 3; i++)
        {
            var item = new QueueMessageItem
            {
                QueueName = TestQueueName,
                Message = $"Waiting item {i}",
                Action = "TEST"
            };
            manager.SubmitRequest(item);
            Assert.IsTrue(manager.Save(item), manager.ErrorMessage);
        }

        var items  = manager.GetRecentQueueItems(TestQueueName).ToList();
        Assert.AreEqual(3, items.Count);

    }


    [TestMethod]
    public void DbPropertyAllowsDirectQueryAccessTest()
    {
        using var manager = CreateManager();
        CleanupQueue(manager);

        var db = manager.Db;
        Assert.IsNotNull(db);

        var collection = db.GetCollection<QueueMessageItem>("QueueMessageItems");
        Assert.IsNotNull(collection);

        var item = new QueueMessageItem
        {
            QueueName = TestQueueName,
            Message = "Direct query test",
            Action = "TEST"
        };
        manager.SubmitRequest(item);
        Assert.IsTrue(manager.Save(item), manager.ErrorMessage);

        var loaded = collection.Find(x => x.Id == item.Id).FirstOrDefault();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(item.Id, loaded.Id);
    }

    private static QueueMessageManagerMongoDb CreateManager()
    {
        return new QueueMessageManagerMongoDb(TestConnectionString)
        {
            AutoCreateTables = true,
            DefaultQueue = TestQueueName
        };
    }

    private static void CleanupQueue(QueueMessageManagerMongoDb manager)
    {
        var collection = manager.Db.GetCollection<QueueMessageItem>("QueueMessageItems");
        collection.DeleteMany(x => x.QueueName == TestQueueName);
    }

    private static bool CanRunMongoTests()
    {
        try
        {
            var manager = new QueueMessageManagerMongoDb(TestConnectionString);
            var collection = manager.Db.GetCollection<QueueMessageItem>("QueueMessageItems");
            _ = collection.EstimatedDocumentCount();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
