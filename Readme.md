#Westwind.QueueMessaging
####.NET Library to provide a simple, two-way messaging queue for enabling offloading of long running operations to other processes/machines####
The purpose of this library is to simplify async processing where long running processes
need to be offloaded to background operations (say in an ASP.NET application) on
seperate threads, external processes or to remote machines. 

Unlike traditional First In First Out queue services this messaging solution allows 
for two-way messaging between the client and the async processing server, to allow for 
progress information, cancelation and completion information between the client and
server doing the async processing. 

This library provides a simple queue message manager that can be used to read and write
message items with simple commands. Messages are popped off the 'queue' and can be 
read and written multiple times, allowing for two-way communication.
Message items are generic and allow for a variety of data inputs and outputs as well
as progress and message information stored which is stored in the data store and
available for reading and writing any time. The queue message manager typically is
used by the client to submit messages and read progress and completion information, 
and by the server to pop off messages, and then write progress and completion messages.

There's also a queue controller implementation that can run as a 'server' and handle 
incoming queue requests on a  configurable number of threads. The controller can
run asynchronously in the background until stopped and pops messages off the queue
and passes them to processing events like StartProcessing(), ExecuteComplete(),
ExecuteFailed().

You can hook up events to the queue controller to handle processing and completion events
explicitly, or subclass the controller implementation and override existing handler 
methods to provide your own self-contained controller implementation. The queue 
controller can run inside of any kind of .NET application - web, console, desktop, 
service or OWin Host applications. This class is optional, but provides a very
simple solution to handling incoming messages from any kind of .NET application
in the background.

![Westwind.MessageQueueing](https://raw.github.com/RickStrahl/Westwind.QueueMessageManager/master/QueueManager_Diagram.png)

###Data Providers###
The implementation of this library is based on replacable data providers using
the QueueMessageManager abstract class. The following providers are provided:

* **QueueMessageManagerSql**
A Sql Server based implementation appropriate for low to medium load of message items.
*(~500 msg/sec for pickups)*

* **QueueMessageManagerMongoDb**
A MongoDb based implementation that is appropriate for high volume of message items.
*(~5000 msg/sec for pickups)*

* **QueueMessageManagerSqlMsMq**
A hybrid implementation that uses MSMQ for actual ID value queueing and data storage
of messages in SQL Server. Uses the same data model used for the QueueMessageManagerSql
but provides much better scalability to avoid locked message retrieval bottlenecks.
Appropriate for high volume of message items.
*(~10000 msg/sec for pickups)*

The library uses a database server table to hold the 'message' information and processing 
instructions that can be accessed repeatedly by both the client and server to provide 
two-way communication during processing of a message. Messages are guaranteed to be picked
up only once, and then are made available to both client and server to allow for status
update information and for reading the actual message data.

The message data is generic so, you can pass any kind of string or string serializable 
data as input or receive it as result output. The message structure also supports 
generic progress messages and completion status.

Both client (QueueMessageManager) and server (QueueController) are provided by this 
library, and the server can be hosted in any kind of .NET application including
console apps, services, Windows desktop apps (WinForms, WPF) and even inside of
a ASP.NET or OWin self-hosted process and run in the background on its own threads.

Because the queue is running using a database server (optionally in conjunction with MSMQ) 
it's easy to scale to any machine that can access a supported db server. 
You can add additional threads, or additional machines to handle the 
remote processing as your load increases.

Currently only supports SQL Server, SQL Compact and MongoDb and a hybrid SQL Server + MSMQ
as data stores.

###Typical Processing###
A typical messaging process with the Queueing components goes like this:

* Client creates a message object and sets its properties to pass data in
  for processing. Typically you set the 'Action' property and one of the
  data fields to pass data like TextInput, BinaryData, Xml, JSON etc.
* Client submits a message with QueueMessageManager and SubmitRequest()
* Messages are identified by a unique ID and an 'action' used for routing
* Server (QueueController) polls for queue messages and pops off any  pending queue items
* Server picks up the message by 'popping off' the next pending Queue message
* Server routes the message for processing to ExecuteStart/OnExecuteStart event/method
* QueueController class implements logic to handle processing of message requests
  by implementing ExecuteStart/ExecuteComplete/ExecuteFaile handlers
* Server starts processing the message asynchronously
* Server optionally writes progress information into Queue record as it processes
* Client can optionally query the message for progress information
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
var manager = new QueueMessageManagerSql();

string imageId = "10";

// Create a message object
// item contains many properties for pushing
// values back and forth as well as a  few message fields
var item = manager.CreateItem();
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
var manager = new QueueMessageManagerSql();

// assume you have held on to the queueId
string queueId = ...;

// load the message 
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

**QueueController is optional**<br/>
Although QueueController is recommended to handle the polling for messages and firing
requests into the event handlers automatically, it's not required. You can also have
two applications pushing and pulling out queue messages on a peer to peer basis and
pick up messages as they need them.

###Implementing a QueueController to process Queued Requests###
The QueueController is a background task that spins up a configured number of threads 
and then pings the queue table for new messages. When new messages arrive it
fires ExecuteStart, ExecuteComplete and ExecuteFailed events that you
can hook your application logic to execute the actual tasks you need to perform - usually 
filtered based on an Action parameter.

It runs continually on a background thread until you stop it, so it's ideal for an 
an always-on 'application server' scenario that can run in a Service type application
or even inside of an ASP.NET Web application started from Application_Start().

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
    var manager = new QueueMessageManagerSql();
    for (int i = 0; i < 3; i++)
    {
        var item = new QueueMessageItem()
        {
            QueueName = "MyQueue",            
            Action = "PRINTIMAGE", // usually used for routing requests in controller
            Message = "Print Image",  // usually used for 'messaging between client and server'
            TextInput = "4334333" // one of the shared data fields here to pass input data
        };

        // sets appropriate settings for submit on item
        manager.SubmitRequest(item);
                
        // item has to be saved
        Assert.IsTrue(manager.Save(), manager.ErrorMessage);
        Console.WriteLine("added " + manager.Item.Id);
    }

    Console.WriteLine("Starting... Async Manager Processing");

    // create the new Controller to process in the background    
    var controller = new QueueController()
    {
       ConnectionString = "QueueMessageManager",
       QueueName = "MyQueue",
        ThreadCount = 2
    };            
    // controller.Initialize(); // read configuration settings
            
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
   

    Console.WriteLine("Stopping... Async Manager Processing");    
}

public int RequestCount = 0; // for testing

/// This is where your processing happens
private void controller_ExecuteStart(QueueMessageManager manager)
{
    // get active queue item
    var item = manager.Item;

    // Typically perform tasks based on some Action/request
    if (item.Action == "PRINTIMAGE")
    {
        // recommend you offload processing
        PrintImage(manager);
    }
    else if (item.Action == "RESIZETHUMBNAIL")
        ResizeThumbnail(manager);

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

    Console.WriteLine(manager.Item.Id + " - Item Completed");
}        
private void controller_ExecuteComplete(QueueMessageManager manager)
{
    // Log or otherwise complete request
    Console.WriteLine("Success: " + manager.Item.Id)
}                
private void controller_ExecuteFailed(QueueMessageManager manager, Exception ex)
{
    Console.WriteLine("Failed (on purpose): " + manager.Item.Id + " - " + ex.Message);
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
          string action = manager.Item.Action;
          if (action == "PrintImage")
              PrintImage(manager);
          else if (action == "CreateThumbnail")
              CreateThumbnail(manager);
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
        var item = manager.Item;
        var id = item.TextInput();
        // do your processing
    }    
}
```

You can then just instantiate and call this custom controller instead.

```C#
var controller = new MyQueueController();
controller.StartProcessingAsync();
```

###Multiple QueueControllers###
You can also run multiplate QueueControllers simultaneously, simply
by configuring multiple QueueController instances pointing at separate
queue names. This allows you to handle multiple operations to run at
seperate isolation levels and queue priorities. For example, you may
have one queue that processes relatively few, but lengthy requests and
another queue that processes very short but quick requests. In order for
the long requests to not hold up slower requests you can have two separate
queues that isolate each from each other.

```C#
var controller = new MyQueueController(){
   QueueName = "Queue1"
};
controller.StartProcessingAsync();

controller2 = new MyQueueController() {
   QueueName = "Queue2"
}
controller2.StartProcessingAsync();

// Typically you'd keep a lasting reference of the controllers
// around for duration of application. Here we simulate by 
// waiting for 10 seconds
Thread.Sleep(100000)


controller.StopProcessing();
controller2.StopProcessing();
```

There's also a QueueControllerMultiple class that allows for creation of
multiple queues that are handled by the same event handlers and which
simplify controlling multiple queues through singular queue start and stop
operations.

```C#
var controller = new QueueControllerMultiple(
    new List<MyQueueController>() {
        new MyQueueController() {
            QueueName = "Queue1",
            ThreadCount = 2
        },
        new MyQueueController() {
            QueueName = "Queue2",
            ThreadCount = 4
        }
    }, "QueueMessageManager");
    
controller.StartProcessingAsync();

Thread.Sleep(100000)

controller.StopProcessing();
```

This starts two separate controllers that use the QueueMessageManager 
connection string to access their respective queues.

###Configuration###
The QueueMessageManager works with a database data store to handle queue messaging. 
Currently Sql Server is supported and we're working on a MongoDb version.
By default the QueueMessageManager uses configuration settings that are stored 
in the configuration file where you specify relevant settings:

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

// Now use the customer configuration
manager = new QueueMessageManagerSql(config);
Console.WriteLine(manager.ConnectionString);
Assert.IsTrue(manager.ConnectionString == "MyApplicationConnectionString");
```

Here's what values are available on the configuration:

*ConnectionString*
The only required value for these settings is the connection string that 
points at the SQL Server instance to hold the data. This value can be
a raw SQL connection string, or - as used above - a ConnectionString
entry in the config <connectionStrings> section. Applies both to 
the manager and controller (via Controller.Initialize()).

*QueueName*
Optional name of the queue that is to be checked for requests to be
processed. The default name is an empty string which checks all queues.
Note that you can have multiple queues and each queue operation can 
be performed on a specific queue. You can override the queue in
all requests individually - the queue name is most important for
the QueueController. Applies both to manager and controller.

*WaitInterval*
The interval in milliseconds to wait between checking for new queue items
if no items are found. If the queue is not empty, a check for the next
item is immediately performed following de-queing of the previous item.
Applies only to controllers.

*ControllerThreads*
The number of threads that the controller uses to process requests.
The number of threads determines how many concurrent queue monitors
ping the queue for new requests. The default is 2. Applies only to
controllers.

##Configuring Multiple Controllers via .config Settings##
If you plan on running multiple controllers to monitor multiple queues simultaneously
you can also configure multiple controllers:

```xml
<QueueManagerConfiguration>
    <add key="ConnectionString" value="QueueMessageManager"/>
    <add key="WaitInterval" value="1000"/>
    <add key="QueueName" value="Queue1"/>
    <add key="ControllerThreads" value="2"/>

    <!-- CONTROLLER LIST BELOW -->
    <add key="Controllers1" value=",TestQueue,2,2000" />
    <add key="Controllers2" value=",TestQueue2,2,5000" />
    <add key="Controllers3" value=",TestQueue3,3,5000" />
</QueueManagerConfiguration>
```

The Controllers List is used with QueueControllerMultiple() which allows a single handler
for several different queues. The list allows specification of controller settings
as a comma delimited list of values. 

The parameters are the connection string, queue name, threadcount and wait interval
respectively. The connection string can be left blank which uses the default connection
string specified above.

###License###
The Westwind.QueueMessaging library is licensed under the MIT License and there's no charge to use, integrate or modify the code for this project. You are free to use it in personal, commercial, government and any other type of application.

Commercial Licenses are also available as an option. If you are using these tools in a commercial application please consider purchasing one of our reasonably priced commercial licenses that help support this project's development.

All source code is copyright West Wind Technologies, regardless of changes made to them. Any source code modifications must leave the original copyright code headers intact.

###Warranty Disclaimer: No Warranty!###

IN NO EVENT SHALL THE AUTHOR, OR ANY OTHER PARTY WHO MAY MODIFY AND/OR REDISTRIBUTE THIS PROGRAM AND DOCUMENTATION, BE LIABLE FOR ANY COMMERCIAL, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES ARISING OUT OF THE USE OR INABILITY TO USE THE PROGRAM INCLUDING, BUT NOT LIMITED TO, LOSS OF DATA OR DATA BEING RENDERED INACCURATE OR LOSSES SUSTAINED BY YOU OR LOSSES SUSTAINED BY THIRD PARTIES OR A FAILURE OF THE PROGRAM TO OPERATE WITH ANY OTHER PROGRAMS, EVEN IF YOU OR OTHER PARTIES HAVE BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.