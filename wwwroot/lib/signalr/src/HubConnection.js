// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { HandshakeProtocol } from "./HandshakeProtocol";
import { MessageType } from "./IHubProtocol";
import { LogLevel } from "./ILogger";
import { Arg, Subject } from "./Utils";
const DEFAULT_TIMEOUT_IN_MS = 30 * 1000;
const DEFAULT_PING_INTERVAL_IN_MS = 15 * 1000;
/** Describes the current state of the {@link HubConnection} to the server. */
export var HubConnectionState;
(function (HubConnectionState) {
    /** The hub connection is disconnected. */
    HubConnectionState[HubConnectionState["Disconnected"] = 0] = "Disconnected";
    /** The hub connection is connected. */
    HubConnectionState[HubConnectionState["Connected"] = 1] = "Connected";
})(HubConnectionState || (HubConnectionState = {}));
/** Represents a connection to a SignalR Hub. */
export class HubConnection {
    /** @internal */
    // Using a public static factory method means we can have a private constructor and an _internal_
    // create method that can be used by HubConnectionBuilder. An "internal" constructor would just
    // be stripped away and the '.d.ts' file would have no constructor, which is interpreted as a
    // public parameter-less constructor.
    static create(connection, logger, protocol) {
        return new HubConnection(connection, logger, protocol);
    }
    constructor(connection, logger, protocol) {
        Arg.isRequired(connection, "connection");
        Arg.isRequired(logger, "logger");
        Arg.isRequired(protocol, "protocol");
        this.serverTimeoutInMilliseconds = DEFAULT_TIMEOUT_IN_MS;
        this.keepAliveIntervalInMilliseconds = DEFAULT_PING_INTERVAL_IN_MS;
        this.logger = logger;
        this.protocol = protocol;
        this.connection = connection;
        this.handshakeProtocol = new HandshakeProtocol();
        this.connection.onreceive = (data) => this.processIncomingData(data);
        this.connection.onclose = (error) => this.connectionClosed(error);
        this.callbacks = {};
        this.methods = {};
        this.closedCallbacks = [];
        this.id = 0;
        this.receivedHandshakeResponse = false;
        this.connectionState = HubConnectionState.Disconnected;
        this.cachedPingMessage = this.protocol.writeMessage({ type: MessageType.Ping });
    }
    /** Indicates the state of the {@link HubConnection} to the server. */
    get state() {
        return this.connectionState;
    }
    /** Starts the connection.
     *
     * @returns {Promise<void>} A Promise that resolves when the connection has been successfully established, or rejects with an error.
     */
    start() {
        return __awaiter(this, void 0, void 0, function* () {
            const handshakeRequest = {
                protocol: this.protocol.name,
                version: this.protocol.version,
            };
            this.logger.log(LogLevel.Debug, "Starting HubConnection.");
            this.receivedHandshakeResponse = false;
            // Set up the promise before any connection is started otherwise it could race with received messages
            const handshakePromise = new Promise((resolve, reject) => {
                this.handshakeResolver = resolve;
                this.handshakeRejecter = reject;
            });
            yield this.connection.start(this.protocol.transferFormat);
            this.logger.log(LogLevel.Debug, "Sending handshake request.");
            yield this.sendMessage(this.handshakeProtocol.writeHandshakeRequest(handshakeRequest));
            this.logger.log(LogLevel.Information, `Using HubProtocol '${this.protocol.name}'.`);
            // defensively cleanup timeout in case we receive a message from the server before we finish start
            this.cleanupTimeout();
            this.resetTimeoutPeriod();
            this.resetKeepAliveInterval();
            // Wait for the handshake to complete before marking connection as connected
            yield handshakePromise;
            this.connectionState = HubConnectionState.Connected;
        });
    }
    /** Stops the connection.
     *
     * @returns {Promise<void>} A Promise that resolves when the connection has been successfully terminated, or rejects with an error.
     */
    stop() {
        this.logger.log(LogLevel.Debug, "Stopping HubConnection.");
        this.cleanupTimeout();
        this.cleanupPingTimer();
        return this.connection.stop();
    }
    /** Invokes a streaming hub method on the server using the specified name and arguments.
     *
     * @typeparam T The type of the items returned by the server.
     * @param {string} methodName The name of the server method to invoke.
     * @param {any[]} args The arguments used to invoke the server method.
     * @returns {IStreamResult<T>} An object that yields results from the server as they are received.
     */
    stream(methodName, ...args) {
        const invocationDescriptor = this.createStreamInvocation(methodName, args);
        let promiseQueue;
        const subject = new Subject();
        subject.cancelCallback = () => {
            const cancelInvocation = this.createCancelInvocation(invocationDescriptor.invocationId);
            const cancelMessage = this.protocol.writeMessage(cancelInvocation);
            delete this.callbacks[invocationDescriptor.invocationId];
            return promiseQueue.then(() => {
                return this.sendMessage(cancelMessage);
            });
        };
        this.callbacks[invocationDescriptor.invocationId] = (invocationEvent, error) => {
            if (error) {
                subject.error(error);
                return;
            }
            else if (invocationEvent) {
                // invocationEvent will not be null when an error is not passed to the callback
                if (invocationEvent.type === MessageType.Completion) {
                    if (invocationEvent.error) {
                        subject.error(new Error(invocationEvent.error));
                    }
                    else {
                        subject.complete();
                    }
                }
                else {
                    subject.next((invocationEvent.item));
                }
            }
        };
        const message = this.protocol.writeMessage(invocationDescriptor);
        promiseQueue = this.sendMessage(message)
            .catch((e) => {
            subject.error(e);
            delete this.callbacks[invocationDescriptor.invocationId];
        });
        return subject;
    }
    sendMessage(message) {
        this.resetKeepAliveInterval();
        return this.connection.send(message);
    }
    /** Invokes a hub method on the server using the specified name and arguments. Does not wait for a response from the receiver.
     *
     * The Promise returned by this method resolves when the client has sent the invocation to the server. The server may still
     * be processing the invocation.
     *
     * @param {string} methodName The name of the server method to invoke.
     * @param {any[]} args The arguments used to invoke the server method.
     * @returns {Promise<void>} A Promise that resolves when the invocation has been successfully sent, or rejects with an error.
     */
    send(methodName, ...args) {
        const invocationDescriptor = this.createInvocation(methodName, args, true);
        const message = this.protocol.writeMessage(invocationDescriptor);
        return this.sendMessage(message);
    }
    /** Invokes a hub method on the server using the specified name and arguments.
     *
     * The Promise returned by this method resolves when the server indicates it has finished invoking the method. When the promise
     * resolves, the server has finished invoking the method. If the server method returns a result, it is produced as the result of
     * resolving the Promise.
     *
     * @typeparam T The expected return type.
     * @param {string} methodName The name of the server method to invoke.
     * @param {any[]} args The arguments used to invoke the server method.
     * @returns {Promise<T>} A Promise that resolves with the result of the server method (if any), or rejects with an error.
     */
    invoke(methodName, ...args) {
        const invocationDescriptor = this.createInvocation(methodName, args, false);
        const p = new Promise((resolve, reject) => {
            // invocationId will always have a value for a non-blocking invocation
            this.callbacks[invocationDescriptor.invocationId] = (invocationEvent, error) => {
                if (error) {
                    reject(error);
                    return;
                }
                else if (invocationEvent) {
                    // invocationEvent will not be null when an error is not passed to the callback
                    if (invocationEvent.type === MessageType.Completion) {
                        if (invocationEvent.error) {
                            reject(new Error(invocationEvent.error));
                        }
                        else {
                            resolve(invocationEvent.result);
                        }
                    }
                    else {
                        reject(new Error(`Unexpected message type: ${invocationEvent.type}`));
                    }
                }
            };
            const message = this.protocol.writeMessage(invocationDescriptor);
            this.sendMessage(message)
                .catch((e) => {
                reject(e);
                // invocationId will always have a value for a non-blocking invocation
                delete this.callbacks[invocationDescriptor.invocationId];
            });
        });
        return p;
    }
    /** Registers a handler that will be invoked when the hub method with the specified method name is invoked.
     *
     * @param {string} methodName The name of the hub method to define.
     * @param {Function} newMethod The handler that will be raised when the hub method is invoked.
     */
    on(methodName, newMethod) {
        if (!methodName || !newMethod) {
            return;
        }
        methodName = methodName.toLowerCase();
        if (!this.methods[methodName]) {
            this.methods[methodName] = [];
        }
        // Preventing adding the same handler multiple times.
        if (this.methods[methodName].indexOf(newMethod) !== -1) {
            return;
        }
        this.methods[methodName].push(newMethod);
    }
    off(methodName, method) {
        if (!methodName) {
            return;
        }
        methodName = methodName.toLowerCase();
        const handlers = this.methods[methodName];
        if (!handlers) {
            return;
        }
        if (method) {
            const removeIdx = handlers.indexOf(method);
            if (removeIdx !== -1) {
                handlers.splice(removeIdx, 1);
                if (handlers.length === 0) {
                    delete this.methods[methodName];
                }
            }
        }
        else {
            delete this.methods[methodName];
        }
    }
    /** Registers a handler that will be invoked when the connection is closed.
     *
     * @param {Function} callback The handler that will be invoked when the connection is closed. Optionally receives a single argument containing the error that caused the connection to close (if any).
     */
    onclose(callback) {
        if (callback) {
            this.closedCallbacks.push(callback);
        }
    }
    processIncomingData(data) {
        this.cleanupTimeout();
        if (!this.receivedHandshakeResponse) {
            data = this.processHandshakeResponse(data);
            this.receivedHandshakeResponse = true;
        }
        // Data may have all been read when processing handshake response
        if (data) {
            // Parse the messages
            const messages = this.protocol.parseMessages(data, this.logger);
            for (const message of messages) {
                switch (message.type) {
                    case MessageType.Invocation:
                        this.invokeClientMethod(message);
                        break;
                    case MessageType.StreamItem:
                    case MessageType.Completion:
                        const callback = this.callbacks[message.invocationId];
                        if (callback != null) {
                            if (message.type === MessageType.Completion) {
                                delete this.callbacks[message.invocationId];
                            }
                            callback(message);
                        }
                        break;
                    case MessageType.Ping:
                        // Don't care about pings
                        break;
                    case MessageType.Close:
                        this.logger.log(LogLevel.Information, "Close message received from server.");
                        // We don't want to wait on the stop itself.
                        // tslint:disable-next-line:no-floating-promises
                        this.connection.stop(message.error ? new Error("Server returned an error on close: " + message.error) : undefined);
                        break;
                    default:
                        this.logger.log(LogLevel.Warning, `Invalid message type: ${message.type}.`);
                        break;
                }
            }
        }
        this.resetTimeoutPeriod();
    }
    processHandshakeResponse(data) {
        let responseMessage;
        let remainingData;
        try {
            [remainingData, responseMessage] = this.handshakeProtocol.parseHandshakeResponse(data);
        }
        catch (e) {
            const message = "Error parsing handshake response: " + e;
            this.logger.log(LogLevel.Error, message);
            const error = new Error(message);
            // We don't want to wait on the stop itself.
            // tslint:disable-next-line:no-floating-promises
            this.connection.stop(error);
            this.handshakeRejecter(error);
            throw error;
        }
        if (responseMessage.error) {
            const message = "Server returned handshake error: " + responseMessage.error;
            this.logger.log(LogLevel.Error, message);
            this.handshakeRejecter(message);
            // We don't want to wait on the stop itself.
            // tslint:disable-next-line:no-floating-promises
            this.connection.stop(new Error(message));
            throw new Error(message);
        }
        else {
            this.logger.log(LogLevel.Debug, "Server handshake complete.");
        }
        this.handshakeResolver();
        return remainingData;
    }
    resetKeepAliveInterval() {
        this.cleanupPingTimer();
        this.pingServerHandle = setTimeout(() => __awaiter(this, void 0, void 0, function* () {
            if (this.connectionState === HubConnectionState.Connected) {
                try {
                    yield this.sendMessage(this.cachedPingMessage);
                }
                catch (_a) {
                    // We don't care about the error. It should be seen elsewhere in the client.
                    // The connection is probably in a bad or closed state now, cleanup the timer so it stops triggering
                    this.cleanupPingTimer();
                }
            }
        }), this.keepAliveIntervalInMilliseconds);
    }
    resetTimeoutPeriod() {
        if (!this.connection.features || !this.connection.features.inherentKeepAlive) {
            // Set the timeout timer
            this.timeoutHandle = setTimeout(() => this.serverTimeout(), this.serverTimeoutInMilliseconds);
        }
    }
    serverTimeout() {
        // The server hasn't talked to us in a while. It doesn't like us anymore ... :(
        // Terminate the connection, but we don't need to wait on the promise.
        // tslint:disable-next-line:no-floating-promises
        this.connection.stop(new Error("Server timeout elapsed without receiving a message from the server."));
    }
    invokeClientMethod(invocationMessage) {
        const methods = this.methods[invocationMessage.target.toLowerCase()];
        if (methods) {
            methods.forEach((m) => m.apply(this, invocationMessage.arguments));
            if (invocationMessage.invocationId) {
                // This is not supported in v1. So we return an error to avoid blocking the server waiting for the response.
                const message = "Server requested a response, which is not supported in this version of the client.";
                this.logger.log(LogLevel.Error, message);
                // We don't need to wait on this Promise.
                // tslint:disable-next-line:no-floating-promises
                this.connection.stop(new Error(message));
            }
        }
        else {
            this.logger.log(LogLevel.Warning, `No client method with the name '${invocationMessage.target}' found.`);
        }
    }
    connectionClosed(error) {
        const callbacks = this.callbacks;
        this.callbacks = {};
        this.connectionState = HubConnectionState.Disconnected;
        // if handshake is in progress start will be waiting for the handshake promise, so we complete it
        // if it has already completed this should just noop
        if (this.handshakeRejecter) {
            this.handshakeRejecter(error);
        }
        Object.keys(callbacks)
            .forEach((key) => {
            const callback = callbacks[key];
            callback(null, error ? error : new Error("Invocation canceled due to connection being closed."));
        });
        this.cleanupTimeout();
        this.cleanupPingTimer();
        this.closedCallbacks.forEach((c) => c.apply(this, [error]));
    }
    cleanupPingTimer() {
        if (this.pingServerHandle) {
            clearTimeout(this.pingServerHandle);
        }
    }
    cleanupTimeout() {
        if (this.timeoutHandle) {
            clearTimeout(this.timeoutHandle);
        }
    }
    createInvocation(methodName, args, nonblocking) {
        if (nonblocking) {
            return {
                arguments: args,
                target: methodName,
                type: MessageType.Invocation,
            };
        }
        else {
            const id = this.id;
            this.id++;
            return {
                arguments: args,
                invocationId: id.toString(),
                target: methodName,
                type: MessageType.Invocation,
            };
        }
    }
    createStreamInvocation(methodName, args) {
        const id = this.id;
        this.id++;
        return {
            arguments: args,
            invocationId: id.toString(),
            target: methodName,
            type: MessageType.StreamInvocation,
        };
    }
    createCancelInvocation(id) {
        return {
            invocationId: id,
            type: MessageType.CancelInvocation,
        };
    }
}
