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
import { AbortController } from "./AbortController";
import { HttpError, TimeoutError } from "./Errors";
import { LogLevel } from "./ILogger";
import { TransferFormat } from "./ITransport";
import { Arg, getDataDetail, sendMessage } from "./Utils";
// Not exported from 'index', this type is internal.
/** @private */
export class LongPollingTransport {
    // This is an internal type, not exported from 'index' so this is really just internal.
    get pollAborted() {
        return this.pollAbort.aborted;
    }
    constructor(httpClient, accessTokenFactory, logger, logMessageContent) {
        this.httpClient = httpClient;
        this.accessTokenFactory = accessTokenFactory;
        this.logger = logger;
        this.pollAbort = new AbortController();
        this.logMessageContent = logMessageContent;
        this.running = false;
        this.onreceive = null;
        this.onclose = null;
    }
    connect(url, transferFormat) {
        return __awaiter(this, void 0, void 0, function* () {
            Arg.isRequired(url, "url");
            Arg.isRequired(transferFormat, "transferFormat");
            Arg.isIn(transferFormat, TransferFormat, "transferFormat");
            this.url = url;
            this.logger.log(LogLevel.Trace, "(LongPolling transport) Connecting.");
            // Allow binary format on Node and Browsers that support binary content (indicated by the presence of responseType property)
            if (transferFormat === TransferFormat.Binary &&
                (typeof XMLHttpRequest !== "undefined" && typeof new XMLHttpRequest().responseType !== "string")) {
                throw new Error("Binary protocols over XmlHttpRequest not implementing advanced features are not supported.");
            }
            const pollOptions = {
                abortSignal: this.pollAbort.signal,
                headers: {},
                timeout: 100000,
            };
            if (transferFormat === TransferFormat.Binary) {
                pollOptions.responseType = "arraybuffer";
            }
            const token = yield this.getAccessToken();
            this.updateHeaderToken(pollOptions, token);
            // Make initial long polling request
            // Server uses first long polling request to finish initializing connection and it returns without data
            const pollUrl = `${url}&_=${Date.now()}`;
            this.logger.log(LogLevel.Trace, `(LongPolling transport) polling: ${pollUrl}.`);
            const response = yield this.httpClient.get(pollUrl, pollOptions);
            if (response.statusCode !== 200) {
                this.logger.log(LogLevel.Error, `(LongPolling transport) Unexpected response code: ${response.statusCode}.`);
                // Mark running as false so that the poll immediately ends and runs the close logic
                this.closeError = new HttpError(response.statusText || "", response.statusCode);
                this.running = false;
            }
            else {
                this.running = true;
            }
            this.receiving = this.poll(this.url, pollOptions);
        });
    }
    getAccessToken() {
        return __awaiter(this, void 0, void 0, function* () {
            if (this.accessTokenFactory) {
                return yield this.accessTokenFactory();
            }
            return null;
        });
    }
    updateHeaderToken(request, token) {
        if (!request.headers) {
            request.headers = {};
        }
        if (token) {
            // tslint:disable-next-line:no-string-literal
            request.headers["Authorization"] = `Bearer ${token}`;
            return;
        }
        // tslint:disable-next-line:no-string-literal
        if (request.headers["Authorization"]) {
            // tslint:disable-next-line:no-string-literal
            delete request.headers["Authorization"];
        }
    }
    poll(url, pollOptions) {
        return __awaiter(this, void 0, void 0, function* () {
            try {
                while (this.running) {
                    // We have to get the access token on each poll, in case it changes
                    const token = yield this.getAccessToken();
                    this.updateHeaderToken(pollOptions, token);
                    try {
                        const pollUrl = `${url}&_=${Date.now()}`;
                        this.logger.log(LogLevel.Trace, `(LongPolling transport) polling: ${pollUrl}.`);
                        const response = yield this.httpClient.get(pollUrl, pollOptions);
                        if (response.statusCode === 204) {
                            this.logger.log(LogLevel.Information, "(LongPolling transport) Poll terminated by server.");
                            this.running = false;
                        }
                        else if (response.statusCode !== 200) {
                            this.logger.log(LogLevel.Error, `(LongPolling transport) Unexpected response code: ${response.statusCode}.`);
                            // Unexpected status code
                            this.closeError = new HttpError(response.statusText || "", response.statusCode);
                            this.running = false;
                        }
                        else {
                            // Process the response
                            if (response.content) {
                                this.logger.log(LogLevel.Trace, `(LongPolling transport) data received. ${getDataDetail(response.content, this.logMessageContent)}.`);
                                if (this.onreceive) {
                                    this.onreceive(response.content);
                                }
                            }
                            else {
                                // This is another way timeout manifest.
                                this.logger.log(LogLevel.Trace, "(LongPolling transport) Poll timed out, reissuing.");
                            }
                        }
                    }
                    catch (e) {
                        if (!this.running) {
                            // Log but disregard errors that occur after stopping
                            this.logger.log(LogLevel.Trace, `(LongPolling transport) Poll errored after shutdown: ${e.message}`);
                        }
                        else {
                            if (e instanceof TimeoutError) {
                                // Ignore timeouts and reissue the poll.
                                this.logger.log(LogLevel.Trace, "(LongPolling transport) Poll timed out, reissuing.");
                            }
                            else {
                                // Close the connection with the error as the result.
                                this.closeError = e;
                                this.running = false;
                            }
                        }
                    }
                }
            }
            finally {
                this.logger.log(LogLevel.Trace, "(LongPolling transport) Polling complete.");
                // We will reach here with pollAborted==false when the server returned a response causing the transport to stop.
                // If pollAborted==true then client initiated the stop and the stop method will raise the close event after DELETE is sent.
                if (!this.pollAborted) {
                    this.raiseOnClose();
                }
            }
        });
    }
    send(data) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.running) {
                return Promise.reject(new Error("Cannot send until the transport is connected"));
            }
            return sendMessage(this.logger, "LongPolling", this.httpClient, this.url, this.accessTokenFactory, data, this.logMessageContent);
        });
    }
    stop() {
        return __awaiter(this, void 0, void 0, function* () {
            this.logger.log(LogLevel.Trace, "(LongPolling transport) Stopping polling.");
            // Tell receiving loop to stop, abort any current request, and then wait for it to finish
            this.running = false;
            this.pollAbort.abort();
            try {
                yield this.receiving;
                // Send DELETE to clean up long polling on the server
                this.logger.log(LogLevel.Trace, `(LongPolling transport) sending DELETE request to ${this.url}.`);
                const deleteOptions = {
                    headers: {},
                };
                const token = yield this.getAccessToken();
                this.updateHeaderToken(deleteOptions, token);
                yield this.httpClient.delete(this.url, deleteOptions);
                this.logger.log(LogLevel.Trace, "(LongPolling transport) DELETE request sent.");
            }
            finally {
                this.logger.log(LogLevel.Trace, "(LongPolling transport) Stop finished.");
                // Raise close event here instead of in polling
                // It needs to happen after the DELETE request is sent
                this.raiseOnClose();
            }
        });
    }
    raiseOnClose() {
        if (this.onclose) {
            let logMessage = "(LongPolling transport) Firing onclose event.";
            if (this.closeError) {
                logMessage += " Error: " + this.closeError;
            }
            this.logger.log(LogLevel.Trace, logMessage);
            this.onclose(this.closeError);
        }
    }
}
