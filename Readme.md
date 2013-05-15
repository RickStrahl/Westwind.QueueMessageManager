#Westwind.QueueMessageManager
###A .NET library for two-way messaging using SQL Server for long running and asynchronous tasks or simple workflows###
This .NET library allows for messaging across application or thread boundaries,
using a SQL Server table. Unlike traditional Message Queues, this implementation
allows for popping off of Queue messages, but also for further direct access messaging, 
to queue messages for cross process communication purposes. In effect this allows
for things like progress information for long running processes and two-way 
communication while a process is running without requiring additional queues.

A typical process goes like this:
* Client submits a message into the MessageQueue

* Server polls for queue messages and pops off any pending queue items
* Server picks up the message by 'popping off' the next Queue message
* Server routes the message for processing to a QueueController class
* QueueController class implements logic to handle incoming messages
* Messages are identified by a unique ID and an 'action' used for routing
* Server starts processing the message asynchronously
* Server optionally writes progress information into Queue record
* Client can optionally query the queue record for progress information
* Server completes processing - updates and 'completes' Queue operation
* Server writes out a result into the Queue table
  (results can be simple values or serialized XML/JSON etc.)
* Client checks and finds completed message and picks up results
  from the Queue table
* Client picks out or deserializes completion data from queue record

How it works
------------
The client application typically interacts with the QueueMessageManager class.
This class provides methods for creating new queue entries, submitting them
to the queue, updating them and then cancelling or completing messages.

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
Once messages have been submitted, a running QueueController will pick them
up and start processing them. If you captures the queueId you can use it to 
load an existing message and access in process properties like PercentComplete
or Message or even some of the data fields to retrieve progress information or
potentially in progress update data.

```C#
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


