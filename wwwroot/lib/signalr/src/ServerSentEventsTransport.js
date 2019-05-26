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
import { LogLevel } from "./ILogger";
import { TransferFormat } from "./ITransport";
import { Arg, getDataDetail, sendMessage } from "./Utils";
/** @private */
export class ServerSentEventsTransport {
    constructor(httpClient, accessTokenFactory, logger, logMessageContent, eventSourceConstructor) {
        this.httpClient = httpClient;
        this.accessTokenFactory = accessTokenFactory;
        this.logger = logger;
        this.logMessageContent = logMessageContent;
        this.eventSourceConstructor = eventSourceConstructor;
        this.onreceive = null;
        this.onclose = null;
    }
    connect(url, transferFormat) {
        return __awaiter(this, void 0, void 0, function* () {
            Arg.isRequired(url, "url");
            Arg.isRequired(transferFormat, "transferFormat");
            Arg.isIn(transferFormat, TransferFormat, "transferFormat");
            this.logger.log(LogLevel.Trace, "(SSE transport) Connecting.");
            // set url before accessTokenFactory because this.url is only for send and we set the auth header instead of the query string for send
            this.url = url;
            if (this.accessTokenFactory) {
                const token = yield this.accessTokenFactory();
                if (token) {
                    url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(token)}`;
                }
            }
            return new Promise((resolve, reject) => {
                let opened = false;
                if (transferFormat !== TransferFormat.Text) {
                    reject(new Error("The Server-Sent Events transport only supports the 'Text' transfer format"));
                    return;
                }
                let eventSource;
                if (typeof window !== "undefined") {
                    eventSource = new this.eventSourceConstructor(url, { withCredentials: true });
                }
                else {
                    // Non-browser passes cookies via the dictionary
                    const cookies = this.httpClient.getCookieString(url);
                    eventSource = new this.eventSourceConstructor(url, { withCredentials: true, headers: { Cookie: cookies } });
                }
                try {
                    eventSource.onmessage = (e) => {
                        if (this.onreceive) {
                            try {
                                this.logger.log(LogLevel.Trace, `(SSE transport) data received. ${getDataDetail(e.data, this.logMessageContent)}.`);
                                this.onreceive(e.data);
                            }
                            catch (error) {
                                this.close(error);
                                return;
                            }
                        }
                    };
                    eventSource.onerror = (e) => {
                        const error = new Error(e.data || "Error occurred");
                        if (opened) {
                            this.close(error);
                        }
                        else {
                            reject(error);
                        }
                    };
                    eventSource.onopen = () => {
                        this.logger.log(LogLevel.Information, `SSE connected to ${this.url}`);
                        this.eventSource = eventSource;
                        opened = true;
                        resolve();
                    };
                }
                catch (e) {
                    reject(e);
                    return;
                }
            });
        });
    }
    send(data) {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.eventSource) {
                return Promise.reject(new Error("Cannot send until the transport is connected"));
            }
            return sendMessage(this.logger, "SSE", this.httpClient, this.url, this.accessTokenFactory, data, this.logMessageContent);
        });
    }
    stop() {
        this.close();
        return Promise.resolve();
    }
    close(e) {
        if (this.eventSource) {
            this.eventSource.close();
            this.eventSource = undefined;
            if (this.onclose) {
                this.onclose(e);
            }
        }
    }
}
