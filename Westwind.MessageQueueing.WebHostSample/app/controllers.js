(function() {
    'use strict';

    angular
        .module('app')
        .controller('queueMonitorController', queueMonitorController);

    queueMonitorController.$inject = ['$scope'];

    function queueMonitorController($scope) {
        /* jshint validthis:true */
        console.log('queuemonitor controller');
        var vm = this;

        vm = $.extend(vm, {
            signalR: {
                hub: null,
                hubUrl: 'http://localhost:8080/signalr',
                baseUrl: "",
                initialQueue: "",
                token: null,
            },
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
            queueNames: [],
            connectionStatus: "Not connected.",
            waitingMessages: 0,
            queueMessages: [],
            activeQueue: ""
        });
        var self = vm;


        vm.initialize = function() {
            // update from parent CSHTML page
            vm.signalR = $.extend(vm.signalR, page);
            
            toastr.timeOut = 4000;
            toastr.options.positionClass = 'toast-bottom-right';

            // popup dialog
            $("#ItemDetail")
                .makeAbsolute()
                .css("z-index", "1000")
                .closable({ cssClass: "closebox-container" })
                .draggable({ handle: ".dialog-header" });

            $("#btnReconnect").click(function() {
                if (vm.signalR.hub == null)
                    toastr.error("Unable to connect. Please refresh the page.");
                else
                    $.connection.hub.start();
            });

            $(document.body).on("click", ".message-item", vm.getQueueMessage);
            
            // get waiting count every 4 secs
            //setInterval(self.getWaitingQueueMessageCount, 3000);

            vm.startHub()
                .done(function() {
                    // *** after we have a connection
                    vm.connectionStatus = "Online";

                    // get initial status from the server (RPC style method)
                    // and bind to UI.            
                    vm.activeQueue = self.initialQueue;
                    vm.getQueueNames();

                    vm.getInitialMessages(vm.signalR.initialQueue);

                    setTimeout(vm.getServiceStatus, 200);
                });
        };


        vm.startHub = function() {
            //jQuery.support.cors = true;
            $.connection.hub.url = vm.signalR.hubUrl; // ie. "http://rasxps/signalR";

            // Pass security token
            $.connection.hub.qs = { "token": vm.signalR.token };
            //$.connection.hub.logging = true;
            //debugger;

            // capture the hub for easier access
            vm.signalR.hub = $.connection.queueMonitorServiceHub;

            var hub = vm.signalR.hub;

            // This means the <script> proxy failed - have to reload
            if (hub == null) {
                vm.connectionStatus = "Offline";
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
                vm.connectionStatus = "Connection lost";
                toastr.error("Connection lost. " + error);

                // IMPORTANT: continuously try re-starting connection - thanks David!
                setTimeout(function() {
                    $.connection.hub.start();
                }, 2000);
            });


            // map client callbacks
            hub.client.writeMessage = vm.writeMessage;            
            hub.client.statusMessage = vm.statusMessage;
            hub.client.getServiceStatusCallback = vm.getServiceStatusCallback;
            hub.client.updateControllerStatusCallback = vm.updateControllerStatusCallback;
            hub.client.getWaitingQueueMessageCountCallback = vm.getWaitingQueueMessageCountCallback;
            hub.client.stopServiceCallback = vm.stopServiceCallback;
            hub.client.startServiceCallback = vm.startServiceCallback;
            hub.client.getQueueMessageCallback = vm.getQueueMessageCallback;
            hub.client.getQueueNamesCallback = vm.getQueueNamesCallback;

            // start the hub and handle after start actions
            // capture promise to return to caller.
            var p = $.connection.hub.start();

            p.done(function() {
                hub.connection.stateChanged(function(change) {
                        if (change.newState === $.signalR.connectionState.reconnecting)
                            vm.connectionStatus = "Connection lost";
                        else if (change.newState === $.signalR.connectionState.connected) {
                            vm.connectionStatus = "Online";

                            // IMPORTANT: On reconnection you have to reset the hub
                            vm.signalR.hub = $.connection.queueMonitorServiceHub;
                        } else if (change.newState === $.signalR.connectionState.disconnected)
                            vm.connectionStatus = "Disconnected";
                    })
                    .error(function(error) {
                        if (!error)
                            error = "Disconnected";
                        toastr.error(error.message);
                    })
                    .disconnected(function(msg) {
                        toastr.warning("Disconnected: " + msg);
                    });
            });

            return p;
        };

        vm.getInitialMessages = function (queue) {
            if (!queue)
                queue = vm.activeQueue;
            if (!queue)
                queue = "";

            vm.activeQueue = queue;
            vm.signalR.hub.server.getInitialMessages(queue)
                .fail(toastr.error);
        }

        // hub callbacks
        vm.writeMessage = function(message, status, time, id, elapsed, waiting, queueName) {
            var div = $("<div>").addClass("message-item").attr("data-id",id);

            if (status && status != "Started") 
                $("#MessageContent>.message-item[data-id=" + id + "]").remove();
            
            self.messageCounter++;
            if (self.messageCounter % 2 == 0)
                div.addClass("alternate");

            if ((vm.activeQueue && queueName) && queueName != vm.activeQueue)
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
                icon = "fa-check' style='color: green'";
            else if (status == "Started")
                icon = "fa-gear fa-spin' style='color: graytext'";
            else if (status == "Cancelled" || status == "Failed" || status == "Error")
                icon = "fa-remove' style='color: red'";
            else
                icon = "fa-info-circle' style='color: steelblue'";

            td.prepend("<i class='fa " + icon + "'></i> ");

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

            if (typeof(waiting) === "number" && waiting > -1) {
                vm.waitingMessages = waiting;
                $scope.$apply();
            }

            $(".message-item").removeClass("alternate");
            $(".message-item:odd").addClass("alternate");
        };
        
        // let the server push messages to the status bar
        vm.statusMessage = function(msg) {
            toastr.info(msg);
        };

        vm.getQueueNames = function () {            
            vm.signalR.hub.server.getQueueNames();
        };

        vm.getQueueNamesCallback = function (queueNames) {
            vm.queueNames = [];
            var items = [];
            items.push({ text: "All Queues", value: "" });
            for (var i = 0; i < queueNames.length; i++) {
                var item = {
                    text: queueNames[i],
                    value: queueNames[i]
                };
                items.push(item);
            }
            
            vm.queueNames = items;            
            $scope.$apply(); // must force SignalR
        };

        vm.getQueueMessage = function() {
            var id = $(this).data("id");
            if (id)
                vm.signalR.hub.server.getQueueMessage(id);
        };
        vm.getQueueMessageCallback = function(qitem) {            
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

            vm.queueItem = item;
            $scope.$apply();

            $("#ItemDetail").show().centerInClient();
        };

        vm.getServiceStatus = function () {
            vm.getInitialMessages();

            vm.signalR.hub.server.getServiceStatus(vm.activeQueue)
                .fail(toastr.error);
        };

        vm.getServiceStatusCallback = function(status) {
            if (!status)
                status = { queueName: "", threadCount: "", waitInterval: "" };

            vm.status = status;
            $scope.$apply(); // must force SignalR
        };

        vm.getWaitingQueueMessageCount = function() {
            vm.signalR.hub.server.getWaitingQueueMessageCount(vm.activeQueue);
        };
        vm.getWaitingQueueMessageCountCallback = function(count) {
            vm.waitingMessages = count;
            $scope.$apply(); // must force SignalR
        };

        vm.stopService =  function () {            
            vm.signalR.hub.server.stopService()
            .done(function() {
                toastr.info("Service stopped.");
            });
        };
        vm.stopServiceCallback = function() {
            vm.status.paused = true;
            $scope.$apply(); // must force SignalR
        };

        vm.startService = function() {
            vm.signalR.hub.server.startService().done(
                function() {
                    toastr.info("Service started.");
                });
        };
        vm.startServiceCallback = function() {
            vm.status.paused = false;
            $scope.$apply(); // must force SignalR
        };

        // UI callbacks
        vm.updateControllerStatus = function() {            
            // decompose model                        
            var status =vm.status;

            if (vm.signalR.hub)
                vm.signalR.hub.server.updateServiceStatus(status)
                    .fail(function() { toastr.error("error"); });
            else
                toastr.error("Server is not available. Update failed.");
        };
        vm.updateControllerStatusCallback = function(status) {            
            // redisplay status settings in ui
            vm.getServiceStatusCallback(status);
        };

        vm.onBtnRefresh = function () {            
            window.location.href = vm.signalR.baseUrl + "?queueName=" + vm.activeQueue;
        }

        // Initialize
        vm.initialize();        
    };
})();
