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
import { DefaultHttpClient } from "./DefaultHttpClient";
import { LogLevel } from "./ILogger";
import { HttpTransportType, TransferFormat } from "./ITransport";
import { LongPollingTransport } from "./LongPollingTransport";
import { ServerSentEventsTransport } from "./ServerSentEventsTransport";
import { Arg, createLogger } from "./Utils";
import { WebSocketTransport } from "./WebSocketTransport";
const MAX_REDIRECTS = 100;
let WebSocketModule = null;
let EventSourceModule = null;
if (typeof window === "undefined" && typeof require !== "undefined") {
    // In order to ignore the dynamic require in webpack builds we need to do this magic
    // @ts-ignore: TS doesn't know about these names
    const requireFunc = typeof __webpack_require__ === "function" ? __non_webpack_require__ : require;
    WebSocketModule = requireFunc("ws");
    EventSourceModule = requireFunc("eventsource");
}
/** @private */
export class HttpConnection {
    constructor(url, options = {}) {
        this.features = {};
        Arg.isRequired(url, "url");
        this.logger = createLogger(options.logger);
        this.baseUrl = this.resolveUrl(url);
        options = options || {};
        options.logMessageContent = options.logMessageContent || false;
        const isNode = typeof window === "undefined";
        if (!isNode && typeof WebSocket !== "undefined" && !options.WebSocket) {
            options.WebSocket = WebSocket;
        }
        else if (isNode && !options.WebSocket) {
            if (WebSocketModule) {
                options.WebSocket = WebSocketModule;
            }
        }
        if (!isNode && typeof EventSource !== "undefined" && !options.EventSource) {
            options.EventSource = EventSource;
        }
        else if (isNode && !options.EventSource) {
            if (typeof EventSourceModule !== "undefined") {
                options.EventSource = EventSourceModule;
            }
        }
        this.httpClient = options.httpClient || new DefaultHttpClient(this.logger);
        this.connectionState = 2 /* Disconnected */;
        this.options = options;
        this.onreceive = null;
        this.onclose = null;
    }
    start(transferFormat) {
        transferFormat = transferFormat || TransferFormat.Binary;
        Arg.isIn(transferFormat, TransferFormat, "transferFormat");
        this.logger.log(LogLevel.Debug, `Starting connection with transfer format '${TransferFormat[transferFormat]}'.`);
        if (this.connectionState !== 2 /* Disconnected */) {
            return Promise.reject(new Error("Cannot start a connection that is not in the 'Disconnected' state."));
        }
        this.connectionState = 0 /* Connecting */;
        this.startPromise = this.startInternal(transferFormat);
        return this.startPromise;
    }
    send(data) {
        if (this.connectionState !== 1 /* Connected */) {
            throw new Error("Cannot send data if the connection is not in the 'Connected' State.");
        }
        // Transport will not be null if state is connected
        return this.transport.send(data);
    }
    stop(error) {
        return __awaiter(this, void 0, void 0, function* () {
            this.connectionState = 2 /* Disconnected */;
            // Set error as soon as possible otherwise there is a race between
            // the transport closing and providing an error and the error from a close message
            // We would prefer the close message error.
            this.stopError = error;
            try {
                yield this.startPromise;
            }
            catch (e) {
                // this exception is returned to the user as a rejected Promise from the start method
            }
            // The transport's onclose will trigger stopConnection which will run our onclose event.
            if (this.transport) {
                yield this.transport.stop();
                this.transport = undefined;
            }
        });
    }
    startInternal(transferFormat) {
        return __awaiter(this, void 0, void 0, function* () {
            // Store the original base url and the access token factory since they may change
            // as part of negotiating
            let url = this.baseUrl;
            this.accessTokenFactory = this.options.accessTokenFactory;
            try {
                if (this.options.skipNegotiation) {
                    if (this.options.transport === HttpTransportType.WebSockets) {
                        // No need to add a connection ID in this case
                        this.transport = this.constructTransport(HttpTransportType.WebSockets);
                        // We should just call connect directly in this case.
                        // No fallback or negotiate in this case.
                        yield this.transport.connect(url, transferFormat);
                    }
                    else {
                        throw Error("Negotiation can only be skipped when using the WebSocket transport directly.");
                    }
                }
                else {
                    let negotiateResponse = null;
                    let redirects = 0;
                    do {
                        negotiateResponse = yield this.getNegotiationResponse(url);
                        // the user tries to stop the connection when it is being started
                        if (this.connectionState === 2 /* Disconnected */) {
                            return;
                        }
                        if (negotiateResponse.error) {
                            throw Error(negotiateResponse.error);
                        }
                        if (negotiateResponse.ProtocolVersion) {
                            throw Error("Detected a connection attempt to an ASP.NET SignalR Server. This client only supports connecting to an ASP.NET Core SignalR Server. See https://aka.ms/signalr-core-differences for details.");
                        }
                        if (negotiateResponse.url) {
                            url = negotiateResponse.url;
                        }
                        if (negotiateResponse.accessToken) {
                            // Replace the current access token factory with one that uses
                            // the returned access token
                            const accessToken = negotiateResponse.accessToken;
                            this.accessTokenFactory = () => accessToken;
                        }
                        redirects++;
                    } while (negotiateResponse.url && redirects < MAX_REDIRECTS);
                    if (redirects === MAX_REDIRECTS && negotiateResponse.url) {
                        throw Error("Negotiate redirection limit exceeded.");
                    }
                    yield this.createTransport(url, this.options.transport, negotiateResponse, transferFormat);
                }
                if (this.transport instanceof LongPollingTransport) {
                    this.features.inherentKeepAlive = true;
                }
                this.transport.onreceive = this.onreceive;
                this.transport.onclose = (e) => this.stopConnection(e);
                // only change the state if we were connecting to not overwrite
                // the state if the connection is already marked as Disconnected
                this.changeState(0 /* Connecting */, 1 /* Connected */);
            }
            catch (e) {
                this.logger.log(LogLevel.Error, "Failed to start the connection: " + e);
                this.connectionState = 2 /* Disconnected */;
                this.transport = undefined;
                throw e;
            }
        });
    }
    getNegotiationResponse(url) {
        return __awaiter(this, void 0, void 0, function* () {
            let headers;
            if (this.accessTokenFactory) {
                const token = yield this.accessTokenFactory();
                if (token) {
                    headers = {
                        ["Authorization"]: `Bearer ${token}`,
                    };
                }
            }
            const negotiateUrl = this.resolveNegotiateUrl(url);
            this.logger.log(LogLevel.Debug, `Sending negotiation request: ${negotiateUrl}.`);
            try {
                const response = yield this.httpClient.post(negotiateUrl, {
                    content: "",
                    headers,
                });
                if (response.statusCode !== 200) {
                    throw Error(`Unexpected status code returned from negotiate ${response.statusCode}`);
                }
                return JSON.parse(response.content);
            }
            catch (e) {
                this.logger.log(LogLevel.Error, "Failed to complete negotiation with the server: " + e);
                throw e;
            }
        });
    }
    createConnectUrl(url, connectionId) {
        if (!connectionId) {
            return url;
        }
        return url + (url.indexOf("?") === -1 ? "?" : "&") + `id=${connectionId}`;
    }
    createTransport(url, requestedTransport, negotiateResponse, requestedTransferFormat) {
        return __awaiter(this, void 0, void 0, function* () {
            let connectUrl = this.createConnectUrl(url, negotiateResponse.connectionId);
            if (this.isITransport(requestedTransport)) {
                this.logger.log(LogLevel.Debug, "Connection was provided an instance of ITransport, using that directly.");
                this.transport = requestedTransport;
                yield this.transport.connect(connectUrl, requestedTransferFormat);
                // only change the state if we were connecting to not overwrite
                // the state if the connection is already marked as Disconnected
                this.changeState(0 /* Connecting */, 1 /* Connected */);
                return;
            }
            const transports = negotiateResponse.availableTransports || [];
            for (const endpoint of transports) {
                this.connectionState = 0 /* Connecting */;
                const transport = this.resolveTransport(endpoint, requestedTransport, requestedTransferFormat);
                if (typeof transport === "number") {
                    this.transport = this.constructTransport(transport);
                    if (!negotiateResponse.connectionId) {
                        negotiateResponse = yield this.getNegotiationResponse(url);
                        connectUrl = this.createConnectUrl(url, negotiateResponse.connectionId);
                    }
                    try {
                        yield this.transport.connect(connectUrl, requestedTransferFormat);
                        this.changeState(0 /* Connecting */, 1 /* Connected */);
                        return;
                    }
                    catch (ex) {
                        this.logger.log(LogLevel.Error, `Failed to start the transport '${HttpTransportType[transport]}': ${ex}`);
                        this.connectionState = 2 /* Disconnected */;
                        negotiateResponse.connectionId = undefined;
                    }
                }
            }
            throw new Error("Unable to initialize any of the available transports.");
        });
    }
    constructTransport(transport) {
        switch (transport) {
            case HttpTransportType.WebSockets:
                if (!this.options.WebSocket) {
                    throw new Error("'WebSocket' is not supported in your environment.");
                }
                return new WebSocketTransport(this.httpClient, this.accessTokenFactory, this.logger, this.options.logMessageContent || false, this.options.WebSocket);
            case HttpTransportType.ServerSentEvents:
                if (!this.options.EventSource) {
                    throw new Error("'EventSource' is not supported in your environment.");
                }
                return new ServerSentEventsTransport(this.httpClient, this.accessTokenFactory, this.logger, this.options.logMessageContent || false, this.options.EventSource);
            case HttpTransportType.LongPolling:
                return new LongPollingTransport(this.httpClient, this.accessTokenFactory, this.logger, this.options.logMessageContent || false);
            default:
                throw new Error(`Unknown transport: ${transport}.`);
        }
    }
    resolveTransport(endpoint, requestedTransport, requestedTransferFormat) {
        const transport = HttpTransportType[endpoint.transport];
        if (transport === null || transport === undefined) {
            this.logger.log(LogLevel.Debug, `Skipping transport '${endpoint.transport}' because it is not supported by this client.`);
        }
        else {
            const transferFormats = endpoint.transferFormats.map((s) => TransferFormat[s]);
            if (transportMatches(requestedTransport, transport)) {
                if (transferFormats.indexOf(requestedTransferFormat) >= 0) {
                    if ((transport === HttpTransportType.WebSockets && !this.options.WebSocket) ||
                        (transport === HttpTransportType.ServerSentEvents && !this.options.EventSource)) {
                        this.logger.log(LogLevel.Debug, `Skipping transport '${HttpTransportType[transport]}' because it is not supported in your environment.'`);
                    }
                    else {
                        this.logger.log(LogLevel.Debug, `Selecting transport '${HttpTransportType[transport]}'.`);
                        return transport;
                    }
                }
                else {
                    this.logger.log(LogLevel.Debug, `Skipping transport '${HttpTransportType[transport]}' because it does not support the requested transfer format '${TransferFormat[requestedTransferFormat]}'.`);
                }
            }
            else {
                this.logger.log(LogLevel.Debug, `Skipping transport '${HttpTransportType[transport]}' because it was disabled by the client.`);
            }
        }
        return null;
    }
    isITransport(transport) {
        return transport && typeof (transport) === "object" && "connect" in transport;
    }
    changeState(from, to) {
        if (this.connectionState === from) {
            this.connectionState = to;
            return true;
        }
        return false;
    }
    stopConnection(error) {
        this.transport = undefined;
        // If we have a stopError, it takes precedence over the error from the transport
        error = this.stopError || error;
        if (error) {
            this.logger.log(LogLevel.Error, `Connection disconnected with error '${error}'.`);
        }
        else {
            this.logger.log(LogLevel.Information, "Connection disconnected.");
        }
        this.connectionState = 2 /* Disconnected */;
        if (this.onclose) {
            this.onclose(error);
        }
    }
    resolveUrl(url) {
        // startsWith is not supported in IE
        if (url.lastIndexOf("https://", 0) === 0 || url.lastIndexOf("http://", 0) === 0) {
            return url;
        }
        if (typeof window === "undefined" || !window || !window.document) {
            throw new Error(`Cannot resolve '${url}'.`);
        }
        // Setting the url to the href propery of an anchor tag handles normalization
        // for us. There are 3 main cases.
        // 1. Relative  path normalization e.g "b" -> "http://localhost:5000/a/b"
        // 2. Absolute path normalization e.g "/a/b" -> "http://localhost:5000/a/b"
        // 3. Networkpath reference normalization e.g "//localhost:5000/a/b" -> "http://localhost:5000/a/b"
        const aTag = window.document.createElement("a");
        aTag.href = url;
        this.logger.log(LogLevel.Information, `Normalizing '${url}' to '${aTag.href}'.`);
        return aTag.href;
    }
    resolveNegotiateUrl(url) {
        const index = url.indexOf("?");
        let negotiateUrl = url.substring(0, index === -1 ? url.length : index);
        if (negotiateUrl[negotiateUrl.length - 1] !== "/") {
            negotiateUrl += "/";
        }
        negotiateUrl += "negotiate";
        negotiateUrl += index === -1 ? "" : url.substring(index);
        return negotiateUrl;
    }
}
function transportMatches(requestedTransport, actualTransport) {
    return !requestedTransport || ((actualTransport & requestedTransport) !== 0);
}
