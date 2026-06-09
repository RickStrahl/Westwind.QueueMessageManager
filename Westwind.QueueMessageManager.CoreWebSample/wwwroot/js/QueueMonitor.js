(() => {
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                hubConnection: null,
                connectionStatus: "Disconnected",
                activeQueue: "",
                queueOptions: [{ text: "All Queues", value: "" }],
                waitingMessages: 0,
                lastStatusMessage: "",
                controllerStatus: {
                    queueName: "",
                    threadCount: 0,
                    waitInterval: 0,
                    paused: false
                },
                messages: [],
                selectedMessage: null,
                messageCounter: 0
            };
        },
        computed: {
            connectionClass() {
                if (this.connectionStatus === "Connected") {
                    return "connected";
                }

                if (this.connectionStatus === "Reconnecting") {
                    return "reconnecting";
                }

                return "disconnected";
            },
            visibleMessages() {
                if (!this.activeQueue) {
                    return this.messages;
                }

                return this.messages.filter(message => {
                    return !message.queueName || message.queueName === this.activeQueue;
                });
            }
        },
        methods: {
            rowClass(status) {
                const normalized = (status || "").toLowerCase();

                if (["success", "ok", "completed"].includes(normalized)) {
                    return "state-success";
                }

                if (["error", "failed", "cancelled"].includes(normalized)) {
                    return "state-error";
                }

                if (["started"].includes(normalized)) {
                    return "state-running";
                }

                return "state-info";
            },
            async initialize() {
                await this.connectHub();
            },
            async connectHub() {
                this.hubConnection = new signalR.HubConnectionBuilder()
                    .withUrl("/queueMonitorServiceHub")
                    .withAutomaticReconnect()
                    .configureLogging(signalR.LogLevel.Warning)
                    .build();

                this.hubConnection.on("writeMessage", (message, status, time, id, elapsed, waiting, queueName, action, obj) => {
                    this.onWriteMessage(message, status, time, id, elapsed, waiting, queueName, action, obj);
                });

                this.hubConnection.on("statusMessage", (message,statusType) => {
                    this.statusMessage(message, statusType);
                });
                this.statusType = "success";
                this.statusMessage = function(message, type) {
                    if (!type)
                        type = "success";

                    this.statusType = type;
                                   
                    this.lastStatusMessage = message || "";
                    if (!this.lastStatusMessage)
                    {
                        this.statusType = "success";
                    }
                    setTimeout(() =>  {
                        this.statusType = "success";
                        this.lastStatusMessage = "";
                    }, 5000);
                };

                this.hubConnection.on("clearMessages", () => {
                    this.clearMessages();
                });

                this.hubConnection.on("getServiceStatusCallback", status => {
                    if (!status) {
                        this.controllerStatus = {
                            queueName: "",
                            threadCount: 0,
                            waitInterval: 0,
                            paused: false
                        };
                        return;
                    }

                    this.controllerStatus = {
                        queueName: status.queueName ?? "",
                        threadCount: status.threadCount ?? 0,
                        waitInterval: status.waitInterval ?? 0,
                        paused: !!status.paused
                    };
                });

                this.hubConnection.on("updateControllerStatusCallback", status => {                    
                    if (!status) {
                        return;
                    }

                    this.controllerStatus = {
                        queueName: status.queueName ?? "",
                        threadCount: status.threadCount ?? 0,
                        waitInterval: status.waitInterval ?? 0,
                        paused: !!status.paused
                    };
                });

                this.hubConnection.on("getWaitingQueueMessageCountCallback", count => {
                    this.waitingMessages = Number.isFinite(count) ? count : 0;
                });

                this.hubConnection.on("stopServiceCallback", () => {
                    this.controllerStatus.paused = true;
                });

                this.hubConnection.on("startServiceCallback", () => {
                    this.controllerStatus.paused = false;
                });

                this.hubConnection.on("getQueueMessageCallback", queueMessage => {                    
                    this.selectedMessage = this.normalizeQueueItem(queueMessage);
                });
                

                this.hubConnection.on("getQueueNamesCallback", queueNames => {
                    const names = Array.isArray(queueNames) ? queueNames : [];
                    this.queueOptions = [{ text: "All Queues", value: "" }].concat(
                        names.map(name => ({ text: name, value: name }))
                    );
                });

                this.hubConnection.onreconnecting(() => {
                    this.connectionStatus = "Reconnecting";
                });

                this.hubConnection.onreconnected(async () => {
                    this.connectionStatus = "Connected";
                    await this.loadStartupData();
                });

                this.hubConnection.onclose(() => {
                    this.connectionStatus = "Disconnected";
                });

                try {
                    await this.hubConnection.start();
                    this.connectionStatus = "Connected";
                    await this.loadStartupData();
                } catch (error) {
                    this.connectionStatus = "Disconnected";
                    this.lastStatusMessage = "Connection failed: " + (error?.message || String(error));
                }
            },
            async loadStartupData() {
                await this.invokeHub("getQueueNames");
                await this.reloadMessages();
                await this.refreshStatus();
            },
            async invokeHub(methodName, ...args) {
                if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
                    this.lastStatusMessage = "Hub is not connected.";
                    return null;
                }

                try {
                    return await this.hubConnection.invoke(methodName, ...args);
                } catch (error) {
                    this.lastStatusMessage = "Hub error: " + (error?.message || String(error));
                    return null;
                }
            },
            async onQueueChanged() {
                await this.reloadMessages();
                await this.refreshStatus();
            },
            async reloadMessages() {
                this.clearMessages();
                //await this.invokeHub("GetInitialMessages", this.activeQueue || "");
                var msgs = await this.invokeHub("GetInitialMessagesList", this.activeQueue || "");
                for (var msg of msgs || []) {                    
                    msg = this.normalizeQueueItem(msg);
                    this.onWriteMessage(msg.message, msg.status, msg.time, msg.id, msg.elapsed, null, msg.queueName, msg.action);
                }
                await this.invokeHub("GetWaitingQueueMessageCount", this.activeQueue || "");
            },
            async refreshStatus() {
                await this.invokeHub("GetServiceStatus", this.activeQueue || "");
            },
            async resetMessage() {     
                this.selectedMessage = await this.invokeHub("ResetRequest", this.selectedMessage?.id);
            },
            clearMessages() {
                this.messages = [];
            },
            onWriteMessage(message, status, time, id, elapsed, waiting, queueName, action) {

                // find the item and remove it if exists
                if (id) {
                    this.messages = this.messages.filter(item => item.id !== id);
                }
                else {
                    id = "_" + Math.random().toString(36).substr(2);
                }
                this.messageCounter += 1;
                const safeId = id;
                const safeMessage = id ? (message || "") : "";
                elapsed = elapsed ? (typeof elapsed === "number" ? `${elapsed.toLocaleString()} ms` : String(elapsed)) : "";

                this.messages.unshift({
                    key: `${safeId}-${this.messageCounter}`,
                    id: safeId,
                    status: status || "Info",
                    time: time || "",
                    elapsed: elapsed || "",
                    queueName: queueName || "",
                    action: action || "",
                    message: safeMessage.trim()
                });

                if (this.messages.length > 60) {
                    this.messages = this.messages.slice(0, 60);
                }

                if (typeof waiting === "number" && waiting > -1) {
                    this.waitingMessages = waiting;
                } else {
                    this.invokeHub("GetWaitingQueueMessageCount", this.activeQueue || "");
                }
            },
            async openMessage(message) {
                if (!message?.id) {
                    return;
                }

                await this.invokeHub("getQueueMessage", message.id);
                await this.on
            },
            normalizeQueueItem(item) {
                if (!item) {
                    return {
                        id: "",
                        status: "",
                        action: "",
                        queueName: "",
                        date: "",
                        time: "",
                        message: "",
                        xml: ""
                    };
                }
                const submitted = item.submitted || item.Submitted || "";
                const date = submitted ? new Date(submitted) : null;
                const time = date ? date.toLocaleTimeString() : "";
                let elapsed = "";
                if (!item.elapsed && item.started && item.completed) {
                    const start = new Date(item.started);
                    const end = new Date(item.completed);
                    elapsed = end - start;
                    if (Number.isFinite(elapsed)) {
                        elapsed = `${elapsed.toLocaleString()} ms`;
                    } else {
                        elapsed = "";
                    }
                }

                return {
                    id: item.id || item.Id || "",
                    status: item.status || item.Status || "",
                    queueName: item.queueName || item.QueueName || "",
                    action: item.action || item.Action || "",
                    date: date && !Number.isNaN(date.getTime()) ? date.toLocaleString() : "",
                    time: time,
                    submitted: submitted,
                    started: item.started && !Number.isNaN(new Date(item.started).getTime()) ? new Date(item.started).toLocaleString() : item.Started || null,
                    completed: item.completed && !Number.isNaN(new Date(item.completed).getTime()) ? new Date(item.completed).toLocaleString() : item.Completed || null,
                    elapsed: elapsed,
                    message: item.message || item.Message || "",
                    xml: item.xml || item.Xml || ""
                };
            },
            async updateControllerStatus() {
                const payload = {
                    queueName: this.controllerStatus.queueName || this.activeQueue,
                    threadCount: Number(this.controllerStatus.threadCount || 0),
                    waitInterval: Number(this.controllerStatus.waitInterval || 0),
                    paused: !!this.controllerStatus.paused
                };

                await this.invokeHub("UpdateServiceStatus", payload);
            },
            async startService() {
                await this.invokeHub("StartService");
            },
            async stopService() {
                await this.invokeHub("StopService");
            }
        },
        async mounted() {
            await this.initialize();
        }
    }).mount("#queueMonitorApp");
})();
