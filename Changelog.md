# Westwind.MessageQueueing Changelog

### Version 0.5
*not released yet*

* **Add Queue Web Host**
Added Hosting components that allow you to host
Queue Controllers easily. ASP.NET Web Host is provided
that allows running the Host controller as an ASP.NET
application.

* **Add Queue Monitor**
Provide sample that can be copied and used for hosting
and that also provides a Queue Monitor that can show
Queue request processing in real time.

* **Add QueueMessageManager.Resubmit()**
Add a method to resubmit a message to the queue for re-processing.
This method clears all process dates and flags and sets status
back to Submitted. For MSMQ it also generates a new MSMQ Id
entry. 