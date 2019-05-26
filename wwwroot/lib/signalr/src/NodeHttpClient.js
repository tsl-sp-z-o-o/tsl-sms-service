// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
import { AbortError, HttpError, TimeoutError } from "./Errors";
import { HttpClient, HttpResponse } from "./HttpClient";
import { LogLevel } from "./ILogger";
import { isArrayBuffer } from "./Utils";
let requestModule;
if (typeof XMLHttpRequest === "undefined") {
    // In order to ignore the dynamic require in webpack builds we need to do this magic
    // @ts-ignore: TS doesn't know about these names
    const requireFunc = typeof __webpack_require__ === "function" ? __non_webpack_require__ : require;
    requestModule = requireFunc("request");
}
export class NodeHttpClient extends HttpClient {
    constructor(logger) {
        super();
        if (typeof requestModule === "undefined") {
            throw new Error("The 'request' module could not be loaded.");
        }
        this.logger = logger;
        this.cookieJar = requestModule.jar();
        this.request = requestModule.defaults({ jar: this.cookieJar });
    }
    send(httpRequest) {
        return new Promise((resolve, reject) => {
            let requestBody;
            if (isArrayBuffer(httpRequest.content)) {
                requestBody = Buffer.from(httpRequest.content);
            }
            else {
                requestBody = httpRequest.content || "";
            }
            const currentRequest = this.request(httpRequest.url, {
                body: requestBody,
                // If binary is expected 'null' should be used, otherwise for text 'utf8'
                encoding: httpRequest.responseType === "arraybuffer" ? null : "utf8",
                headers: Object.assign({ 
                    // Tell auth middleware to 401 instead of redirecting
                    "X-Requested-With": "XMLHttpRequest" }, httpRequest.headers),
                method: httpRequest.method,
                timeout: httpRequest.timeout,
            }, (error, response, body) => {
                if (httpRequest.abortSignal) {
                    httpRequest.abortSignal.onabort = null;
                }
                if (error) {
                    if (error.code === "ETIMEDOUT") {
                        this.logger.log(LogLevel.Warning, `Timeout from HTTP request.`);
                        reject(new TimeoutError());
                    }
                    this.logger.log(LogLevel.Warning, `Error from HTTP request. ${error}`);
                    reject(error);
                    return;
                }
                if (response.statusCode >= 200 && response.statusCode < 300) {
                    resolve(new HttpResponse(response.statusCode, response.statusMessage || "", body));
                }
                else {
                    reject(new HttpError(response.statusMessage || "", response.statusCode || 0));
                }
            });
            if (httpRequest.abortSignal) {
                httpRequest.abortSignal.onabort = () => {
                    currentRequest.abort();
                    reject(new AbortError());
                };
            }
        });
    }
    getCookieString(url) {
        return this.cookieJar.getCookieString(url);
    }
}
