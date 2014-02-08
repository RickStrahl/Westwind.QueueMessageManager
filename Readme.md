#Westwind.QueueMessageManager
####.NET Library to provide a simple, two-way messaging queue for enabling offloading of long running operations to other processes/machines####
This .NET library provides a simple and easy to implement mechanism for  offloading 
async processing for long running or CPU intensive tasks. It also provides two-way status
and progress messaging. The library uses a SQL Server table to hold the 'message' 
information and processing instructions that can be accessed repeatedly by both the 
client and server to provide two way communication during processing of a message.

The message data is generic so, you can pass any kind of string or string serializable 
data as input or receive it as result output. The message structure also supports 
generic progress messages and completion status.

The purpose of this library is to simplify async processing where long running processes
need to be offloaded either to background threads (say in an ASP.NET application), or 
to remote machines for async processing. The client can then check back for completion
and/or progress information if the server provides it.

Both client (QueueMessageManager) and server (QueueController )are provided by this 
library, and the server can be hosted in any kind of application and run in the background.

Because the queue is running in SQL Server it's easy to scale to any machine that can
access the SQL Server that hosts the messaging table. You can add additional
threads, or additional machines to handle the remote processing as your load increases.
 
Unlike traditional Message Queues, this implementation allows for read/write access to 
message items so that progress information can be shared as well as having an easy mechanism
for determining completion status which is non-trivial with pure Queue implementations.

Currently only supports SQL Server and SQL Compact.

A typical messaging process with the Queueing components goes like this:

* Client submits a message with QueueMessageManager and SubmitRequest()
* Server (QueueController) polls for queue messages and pops off any pending queue items
* Server picks up the message by 'popping off' the next pending Queue message
* Server routes the message for processing to ExecuteStart() method
* QueueController class implements logic to handle processing of message requests
* Messages are identified by a unique ID and an 'action' used for routing
* Server starts processing the message asynchronously
* You implement ExecuteStart handler that performs async or remote processing task
* Server optionally writes progress information into Queue record as it processes
* Client can optionally query the queue record for progress information
* Server completes request and optionally writes result data into record
* Server fires ExecuteComplete or ExecuteFailed
* Client checks and finds completed message and picks up results
  from the Queue table
* Client picks out or deserializes completion data from queue record

###How it works###
The client application interacts with the *QueueMessageManager* class to create, 
update and manage messages submitted to the queue. Clients typically create
a message for processing, then check back occasionally for status updates
and completion or cancellation.

The server application runs in the background as a service, daemon or simply
on a separate thread either on the local or remote machine. The server picks up 
messages and processes them, which allows for asynchronous offloading of 
processing to a separate process or machine. The *QueueController* is a 
base class that provides for message polling, firing events when messages arrive. 
The implementation subclasses QueueController and overrides the various messaging 
handler methods like OnExecuteStart(),  OnExecuteComplete() or OnExecuteFailed()
to hook up custom processing. Operations running can also interact with the 
manager to provide progress and status information to the client.

###Creating and interacting with Messages via QueueMessageManager###
The QueueMessageManager class provides methods for creating new queue entries and
submitting them to the queue, for updating them and then cancelling or 
completing messages.

Typical client submission code looks like this:

```C#
var manager = new QueueMessageManager();

string imageId = "10";

// Create a message object
// item contains many properties for pushing
// values back and forth as well as a  few message fields
var item = manager.NewEntity();
item.Action = "PRINTIMAGE";
item.TextInput = imageId;
item.Message = "Print Image operation started at " + DateTime.Now.ToString();
item.PercentComplete = 10;

// *** you can also serialize objects directly into the Xml property
// manager.Serialization.SerializeToXml(SomeObjectToSerialize);

// add an arbitrary custom properties - serialized to Xml
manager.Properties.Add("Time", DateTime.Now);
manager.Properties.Add("User", "ricks");

// Set the message status and timestamps as submitted             
manager.SubmitRequest(item);

// actually save the queue message to disk
Assert.IsTrue(manager.Save(), manager.ErrorMessage);

// you can capture the message ID use it
// to load messages later
var queueId = item.Id;
```

Once messages have been submitted, a running QueueController can pick them
up and start processing them.

If you capture the queueId you can use it to load an existing message and 
access in process properties like PercentComplete or Message or even some 
of the data fields to retrieve progress information or potentially in 
progress update data.

```C#
var manager = new QueueMessageManager();

// assume you have a queueId
string queueId = ...;

// load the message 
var manager = new QueueMessageManager();
item = manager.Load(queueId);

Assert.IsNotNull(item, manager.ErrorMessage);

if (item.Completed)
{
	// pick up a result value from one of
    // of the data or serialized fields
    string result = item.TextResult;
    
    DoSomeThingWithResult(result);
    
	return;
}

// Otherwise update the message any way you like
item.Message = "Updated @ " + DateTime.Now.ToString("t");
item.PercentComplete = 10;

// Save the the updated message to disk
Assert.IsTrue(manager.Save(), manager.ErrorMessage);

// or you can use high level methods
manager.UpdateQueueMessageStatus(messageText: "Updated @ " + DateTime.Now.ToString(), percentComplete: 10, autoSave: true);
```

Note that both client and server can write to the queue message
and so pass messages back and forth. There are a number of fields
available to hold input and output data as well as serialized 
data both in XML and binary form, plus you can use the Properties
collection to push arbitrary values into the message.

The server typically runs a subclass of QueueController which is a multi-threaded
polling operation that looks for queue messages. When a queue message is found the
QueueController fires a series of events - ExecuteStart, ExecuteComplete, ExecuteFailed -
to notify of new incoming messages to process. The customized code can then examine
the QueueMessageItem for its properties to determine how to process the message.
Typically an Action can be set on the QueueMessageItem to route processing.

###Implementing a QueueController to process Queued Requests###
The QueueController is a background task that spins up several threads and
then pings the queue table for new messages. When new messages arrive it
fires ExecuteStart, ExecuteComplete and ExecuteFailed events that you
can hook your application logic to.

The QueueController can be plugged into any kind of application as long
as the application has a lifetime to keep the controller alive. This can
be as part of an ASP.NET application (loaded from Application_Start then
runnning in the background) or from a service that loads the component on
startup. Note that you're responsible for keeping the Controller instance
alive by attaching it to a global or static property that persists until the
application is ready to terminate.

Here's what this looks like:

```C#
[TestMethod]
public void QueueControllerTest()
{
    // for testing - submit 3 client messages
    var manager = new QueueMessageManager();
    for (int i = 0; i < 3; i++)
    {
        var item = new QueueMessageItem()
        {
            Message = "Print Image",
            Action = "PRINTIMAGE",
            TextInput = "4334333" // image Id
        };

        // sets appropriate settings for submit on item
        manager.SubmitRequest(item);
                
        // item has to be saved
        Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        Console.WriteLine("added " + manager.Entity.Id);
    }

    Console.WriteLine("Starting... Async Manager Processing");

    // create the new Controller to process in the background    
    var controller = new QueueController()
    {
        ThreadCount = 2
    };            
            
    // ExecuteStart Event is where your processing logic goes
    controller.ExecuteStart += controller_ExecuteStart;

    // ExecuteFailed and ExecuteComplete let you take actions on completion
    controller.ExecuteComplete += controller_ExecuteComplete;
    controller.ExecuteFailed += controller_ExecuteFailed;
    
    controller.StartProcessingAsync();
            
    // For test we have to keep the threads alive 
    // to allow the 3 requests to process
    Thread.Sleep(2000);

    // shut down
    controller.StopProcessing();
    Thread.Sleep(1000);  // let threads shut down

    Console.WriteLine("Stopping... Async Manager Processing");    
}

public int RequestCount = 0; // for testing

/// This is where your processing happens
private void controller_ExecuteStart(QueueMessageManager manager)
{
    // get active queue item
    var item = manager.Entity;

    // Typically perform tasks based on some Action/request
    if (item.Action == "PRINTIMAGE")
    {
        // recommend you offload processing
        //PrintImage(manager);
    }
    else if (item.Action == "RESIZETHUMBNAIL")
        //ResizeThumbnail(manager);

    // just for kicks
    Interlocked.Increment(ref RequestCount);

    // third request should throw exception, trigger ExecuteFailed            
    if (RequestCount > 2)
    {
        // throw an Execption through code failure
        object obj = null;
        obj.ToString();
    }

    // Complete request 
    manager.CompleteRequest(messageText: "Completed request " + DateTime.Now, 
                            autoSave: true);

    Console.WriteLine(manager.Entity.Id + " - Item Completed");
}        
private void controller_ExecuteComplete(QueueMessageManager manager)
{
    // Log or otherwise complete request
    Console.WriteLine("Success: " + manager.Entity.Id)
}                
private void controller_ExecuteFailed(QueueMessageManager manager, Exception ex)
{
    Console.WriteLine("Failed (on purpose): " + manager.Entity.Id + " - " + ex.Message);
}
```

Another approach is to subclass the QueueController and add your processing logic into
this class. You can override the OnExecuteStart, OnExecuteFailed, OnExecuteComplete
handlers the same as above and then add all your processing logic in methods of
this class. This is the recommended approach.

```C#
public class MyController : QueueController
{
    protected override OnExecuteStart(QueueMessageManager manager)
    {
          string action = manager.Entity.Action;
          if (action == "PrintImage")
              PrintImage(manager);
          else if (action == "CreateThumbnail")
              //CreateThumbnail(manager);
    }
    protected override OnExecuteComplete(QueueMessageManager manager)
    {
          ...
    }
    protected override OnExecuteFailed(QueueMessageManager manager)
    {
         ...
    }
    
    private void PrintImage(QueueMessageManager manager)
    {
        var item = manager.Entity;
        // do your processing
    }    
}
```

You can then just instantiate and call this custom controller instead.

```C#
var controller = new MyQueueController();
controller.StartProcessingAsync();
```

###Configuration###
The QueueMessageManager works with Sql Server to handle queue messaging. By
default the QueueMessageManager uses configuration settings that are stored in the
configuration file where you specify relevant settings:

```xml
<QueueManagerConfiguration>
	<add key="ConnectionString" value="ApplicationConfigurationString" />
	<add key="WaitInterval" value="1000" />
	<add key="QueueName" value="DefaultQueue" />
	<add key="ControllerThreads" value="2" />
</QueueManagerConfiguration>
 ```

By default settings are read out of the config file and settings are auto-created
if they don't exist (assuming the application has rights to write). But you can also 
explicitly set these values by passing a QueueMessageManagerConfiguration()
object with configuration settings preset into the constructor:  

 ```c#
var config = new QueueMessageManagerConfiguration()
{                 
    ConnectionString = "MyApplicationConnectionString",
    ControllerThreads = 10
};
manager = new QueueMessageManager(config);
Console.WriteLine(manager.ConnectionString);
Assert.IsTrue(manager.ConnectionString == "MyApplicationConnectionString");
```

Here's what values are available on the configuration:

*ConnectionString*
The only required value for these settings is the connection string that 
points at the SQL Server instance to hold the data. This value can be
a raw SQL connection string, or - as used above - a ConnectionString
entry in the config <connectionStrings> section.

*QueueName*
Optional name of the queue that is to be checked for requests to be
processed. The default name is an empty string which checks all queues.
Note that you can have multiple queues and each queue operation can 
be performed on a specific queue.

*WaitInterval*
The interval in milliseconds to wait between checking for new queue items
if no items are found. If the queue is not empty, a check for the next
item is immediately performed following de-queing of the previous item.

*ControllerThreads*
The number of threads that the controller uses to process requests.
The number of threads determines how many concurrent queue monitors
ping the queue for new requests. The default is 2.

###License###