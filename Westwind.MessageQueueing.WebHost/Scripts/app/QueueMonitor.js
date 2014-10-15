var Page = function () {
    var self = this;

    $.extend(this, {
        // hub and connection properties
        hub: null,
        hubUrl: 'http://localhost:8080/signalr',
        initialQueue: '',
        token: null,

        viewModel: {
            status: {
                queueName: "",
                threadCount: 0,
                waitInterval: 0,
                paused: false,
                serviceStatus: "Running"
            },
            queueItem: {
                id: "",
                date: new Date(),
                status: "",
                message: "",
                xml: ""
            },
            queueNames: ko.observableArray([]),
            connectionStatus: ko.observable("Not connected."),
            waitingMessages: ko.observable(0),
            queueMessages: ko.observableArray([]),
            activeQueue: ko.observable("")
        },

        // methods
        initialize: function() {
            showStatus({ autoClose: true, closable: true });
            toastr.timeOut = 4000;

            // make observable
            self.viewModel.status = ko.mapping.fromJS(self.viewModel.status);
            self.viewModel.queueItem = ko.mapping.fromJS(self.viewModel.queueItem);
            self.viewModel.activeQueue(page.initialQueue);

            $("#ItemDetail")
                .makeAbsolute()
                .css("z-index", "1000")
                .closable({ cssClass: "closebox-container" })
                .draggable({ handle: ".dialog-header" });

            $("#btnReconnect").click(function() {
                if (self.hub == null)
                    toastr.error("Unable to connect. Please refresh the page.");
                else
                    $.connection.hub.start();
            });

            $(document.body).on("click", ".message-item", self.getQueueMessage);
            $("#btnUpdateStatus").click(self.btnUpdateStatus);
            $("#btnRefresh").click(self.btnRefresh);
            $(document).on("click", "#btnStopService", self.stopService);
            $(document).on("click", "#btnStartService", self.startService);

            // get waiting count every 4 secs
            setInterval(self.getWaitingQueueMessageCount, 3000);

            
            // status bindings
            ko.applyBindings(self.viewModel);

            $(document).on("change", "#lstQueues", function() {
                self.getServiceStatus();
            });
        },

        startHub: function() {
            //jQuery.support.cors = true;
            $.connection.hub.url = self.hubUrl; // ie. "http://rasxps/signalR";

            // Pass security token
            $.connection.hub.qs = { "token": page.token };
            //$.connection.hub.logging = true;
            //debugger;

            // capture the hub for easier access
            self.hub = $.connection.queueMonitorServiceHub;

            var hub = self.hub;

            // This means the <script> proxy failed - have to reload
            if (hub == null) {
                self.viewModel.connectionStatus("Offline");
                toastr.error("Couldn't connect to server. Please refresh the page.");
                return;
            }

            // Connection Events
            hub.connection.error(function(error) {
                if (error)
                    toastr.error("An error occurred: " + error.message);
                self.hub = null;
            });
            hub.connection.disconnected(function(error) {
                self.viewModel.connectionStatus("Connection lost");
                toastr.error("Connection lost. " + error);

                // IMPORTANT: continuously try re-starting connection - thanks David!
                setTimeout(function() {
                    $.connection.hub.start();
                }, 2000);
            });


            // map client callbacks
            hub.client.writeMessage = self.writeMessage;
            hub.client.writeQueueMessage = self.writeQueueMessage;
            hub.client.statusMessage = self.statusMessage;
            hub.client.getServiceStatusCallback = self.getServiceStatusCallback;
            hub.client.updateServiceStatusCallback = self.updateServiceStatusCallback;
            hub.client.getWaitingQueueMessageCountCallback = self.getWaitingQueueMessageCountCallback;
            hub.client.stopServiceCallback = self.stopServiceCallback;
            hub.client.startServiceCallback = self.startServiceCallback;
            hub.client.getQueueMessageCallback = self.getQueueMessageCallback;
            hub.client.getQueueNamesCallback = self.getQueueNamesCallback;


            // start the hub and handle after start actions
            $.connection.hub
                .start()
                .done(function() {
                    hub.connection.stateChanged(function(change) {
                            if (change.newState === $.signalR.connectionState.reconnecting)
                                self.viewModel.connectionStatus("Connection lost");
                            else if (change.newState === $.signalR.connectionState.connected) {
                                self.viewModel.connectionStatus("Online");

                                // IMPORTANT: On reconnection you have to reset the hub
                                self.hub = $.connection.queueMonitorServiceHub;
                            } else if (change.newState === $.signalR.connectionState.disconnected)
                                self.viewModel.connectionStatus("Disconnected");
                        })
                        .error(function(error) {
                            if (!error)
                                error = "Disconnected";
                            toastr.error(error.message);
                        })
                        .disconnected(function(msg) {
                            toastr.warning("Disconnected: " + msg);
                        });

                    self.viewModel.connectionStatus("Online");

                    // get initial status from the server (RPC style method)
                    // and bind to UI.
                    self.viewModel.activeQueue(self.initialQueue);
                    self.getQueueNames();
                    self.getInitialMessages();
                    setTimeout(self.getServiceStatus, 200);
                });
        },

        messageCounter: 0,

        // hub callbacks
        writeMessage: function(message, status, time, id, elapsed, waiting, queueName) {
            var div = $("<div>").addClass("message-item");

            self.messageCounter++;
            if (self.messageCounter % 2 == 0)
                div.addClass("alternate");

            if ((self.viewModel.activeQueue() && queueName) && queueName != self.viewModel.activeQueue())
                return;

            // TODO: move to knockout template
            if (!id) {
                // display the message in the ID field
                id = message;
                // display no message below
                message = null;
            } else
                div.data("id", id);

            if (!time)
                time = new Date().formatDate("HH:mm:ss`");


            // create message layout table - yuk!
            var table = $("<table>").addClass("message-table");
            var tr = $("<tr>");
            
            var td = $("<td>").addClass("message-status")
                .text(status); 
            td.appendTo(tr);

            var icon = "";
            if (status == "Completed" || status == "Ok" || status == "Success")
                icon = "icon-ok";
            else if (status == "Started")
                icon = "icon-running";
            else if (status == "Cancelled" || status == "Failed" || status == "Error")
                icon = "icon-error";
            else
                icon = "icon-info";

            td.prepend("<span class='" + icon + "'></span>");

            td = $("<td>").addClass("message-time")
                    .text(time);
            

            td.appendTo(tr);

            td = $("<td>").text(id);
            td.appendTo(tr);

            var msgElapsed = "";
            if (queueName)
                msgElapsed += "<b>" + queueName + "</b> | ";
            if (elapsed) {
                if (msgElapsed)
                    msgElapsed += elapsed;
                else
                    msgElapsed = elapsed;
            }

            td = $("<td>").addClass("message-elapsed")
                .html(msgElapsed);
            tr.append(td);
            tr.appendTo(table);

            if (message) {
                tr = $("<tr>");
                td = $("<td>").attr("colspan", "1");
                td.appendTo(tr);

                message = "<pre style='font-size: 8pt;padding: 3px 5px;margin: 0'>" + message.trim() + "</pre>";
                td = $("<td>").attr("colspan","3").css("text-overflow","clip");
                if (message.indexOf("/>") > 0 || message.indexOf("</") > 0)
                    td.html(message);
                else
                    td.text(message);

                td.appendTo(tr);

                tr.appendTo(table);
            }

            table.appendTo(div);


            div.prependTo("#MessageContent");

            var items = $("#MessageContent>div");
            if (items.length > 25) {
                for (var i = 20; i < items.length; i++) {
                    var item = items[i];
                    if (item != null)
                        $(item).remove();
                }
            }

            if (typeof(waiting) === "number" && waiting > -1)
                self.viewModel.waitingMessages(waiting);
        },
        
        // let the server push messages to the status bar
        statusMessage: function(msg) {
            toastr.info(msg);
        },

        getInitialMessages: function() {
            self.hub.server.getInitialMessages(self.initialQueue)
                .fail(toastr.error);
        },
        getServiceStatus: function () {                        
            self.hub.server.getServiceStatus(self.viewModel.activeQueue())           
                .fail(toastr.error);
        },
        getServiceStatusCallback: function (status) {            
            var viewModel = self.viewModel;
            if (!status) 
                status = { queueName: "", threadCount: "", waitInterval: "" };
            
            ko.mapping.fromJS(status, viewModel.status);
        },
        stopService: function () {            
            self.hub.server.stopService();
        },
        stopServiceCallback: function() {
            self.viewModel.status.paused(true);
        },
        startService: function() {
            self.hub.server.startService();
        },
        startServiceCallback: function() {
            self.viewModel.status.paused(false);
        },
        updateServiceStatusCallback: function(status) {
            var viewModel = self.viewModel;

            // redisplay status settings in ui
            self.getServiceStatusCallback(status);
        },
        getQueueMessage: function () {
            var id = $(this).data("id");            
            if (id)
                self.hub.server.getQueueMessage(id);
        },
        getQueueMessageCallback: function(qitem) {            
            if (!qitem)
                return;
            
            var d, dt = JSON.dateStringToDate(qitem.Submitted);            
            if (dt)
                d= dt.formatDate("MMM dd, HH:mm");            
            
            
            var item = {
                id: qitem.Id,
                date: d,
                status: qitem.Status,
                message: qitem.Message,
                xml: qitem.Xml
            };
            
            var viewModel = self.viewModel;
            ko.mapping.fromJS(item, viewModel.queueItem);

            $("#ItemDetail").show().centerInClient();
        },
        getQueueNames: function () {            
            self.hub.server.getQueueNames();
        },
        getQueueNamesCallback: function (queueNames) {            
            var items = [];
            items.push({ text: "All Queues", value: "" });            
            for (var i = 0; i < queueNames.length; i++) {
                var item = {
                    text: queueNames[i],
                    value: queueNames[i]
                };
                items.push(item);
            }
            self.viewModel.queueNames(items);
            self.viewModel.activeQueue(self.initialQueue);                        
        },
        getWaitingQueueMessageCount: function () {            
            self.hub.server.getWaitingQueueMessageCount(self.viewModel.activeQueue());
        },
        getWaitingQueueMessageCountCallback: function(count) {
            // assign to viewmodel - knockout will bind
            self.viewModel.waitingMessages(count);
        },

        // UI callbacks
        btnUpdateStatus: function () {            
            // decompose model                        
            var status = ko.mapping.toJS(self.viewModel.status);
            
            if (self.hub)
                self.hub.server.updateServiceStatus(status)
                        .fail(toastr.error);
            else
                toastr.error("Server is not available. Update failed.");
        },
        btnRefresh: function() {
            window.location.href = page.baseUrl + "?queueName=" + self.viewModel.activeQueue();
        }


    });    

    return self;
};