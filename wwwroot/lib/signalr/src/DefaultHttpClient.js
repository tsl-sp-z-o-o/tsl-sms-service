// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
import { AbortError } from "./Errors";
import { HttpClient } from "./HttpClient";
import { NodeHttpClient } from "./NodeHttpClient";
import { XhrHttpClient } from "./XhrHttpClient";
/** Default implementation of {@link @aspnet/signalr.HttpClient}. */
export class DefaultHttpClient extends HttpClient {
    /** Creates a new instance of the {@link @aspnet/signalr.DefaultHttpClient}, using the provided {@link @aspnet/signalr.ILogger} to log messages. */
    constructor(logger) {
        super();
        if (typeof XMLHttpRequest !== "undefined") {
            this.httpClient = new XhrHttpClient(logger);
        }
        else {
            this.httpClient = new NodeHttpClient(logger);
        }
    }
    /** @inheritDoc */
    send(request) {
        // Check that abort was not signaled before calling send
        if (request.abortSignal && request.abortSignal.aborted) {
            return Promise.reject(new AbortError());
        }
        if (!request.method) {
            return Promise.reject(new Error("No method defined."));
        }
        if (!request.url) {
            return Promise.reject(new Error("No url defined."));
        }
        return this.httpClient.send(request);
    }
    getCookieString(url) {
        return this.httpClient.getCookieString(url);
    }
}
