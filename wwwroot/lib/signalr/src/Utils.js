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
import { NullLogger } from "./Loggers";
/** @private */
export class Arg {
    static isRequired(val, name) {
        if (val === null || val === undefined) {
            throw new Error(`The '${name}' argument is required.`);
        }
    }
    static isIn(val, values, name) {
        // TypeScript enums have keys for **both** the name and the value of each enum member on the type itself.
        if (!(val in values)) {
            throw new Error(`Unknown ${name} value: ${val}.`);
        }
    }
}
/** @private */
export function getDataDetail(data, includeContent) {
    let detail = "";
    if (isArrayBuffer(data)) {
        detail = `Binary data of length ${data.byteLength}`;
        if (includeContent) {
            detail += `. Content: '${formatArrayBuffer(data)}'`;
        }
    }
    else if (typeof data === "string") {
        detail = `String data of length ${data.length}`;
        if (includeContent) {
            detail += `. Content: '${data}'`;
        }
    }
    return detail;
}
/** @private */
export function formatArrayBuffer(data) {
    const view = new Uint8Array(data);
    // Uint8Array.map only supports returning another Uint8Array?
    let str = "";
    view.forEach((num) => {
        const pad = num < 16 ? "0" : "";
        str += `0x${pad}${num.toString(16)} `;
    });
    // Trim of trailing space.
    return str.substr(0, str.length - 1);
}
// Also in signalr-protocol-msgpack/Utils.ts
/** @private */
export function isArrayBuffer(val) {
    return val && typeof ArrayBuffer !== "undefined" &&
        (val instanceof ArrayBuffer ||
            // Sometimes we get an ArrayBuffer that doesn't satisfy instanceof
            (val.constructor && val.constructor.name === "ArrayBuffer"));
}
/** @private */
export function sendMessage(logger, transportName, httpClient, url, accessTokenFactory, content, logMessageContent) {
    return __awaiter(this, void 0, void 0, function* () {
        let headers;
        if (accessTokenFactory) {
            const token = yield accessTokenFactory();
            if (token) {
                headers = {
                    ["Authorization"]: `Bearer ${token}`,
                };
            }
        }
        logger.log(LogLevel.Trace, `(${transportName} transport) sending data. ${getDataDetail(content, logMessageContent)}.`);
        const responseType = isArrayBuffer(content) ? "arraybuffer" : "text";
        const response = yield httpClient.post(url, {
            content,
            headers,
            responseType,
        });
        logger.log(LogLevel.Trace, `(${transportName} transport) request complete. Response status: ${response.statusCode}.`);
    });
}
/** @private */
export function createLogger(logger) {
    if (logger === undefined) {
        return new ConsoleLogger(LogLevel.Information);
    }
    if (logger === null) {
        return NullLogger.instance;
    }
    if (logger.log) {
        return logger;
    }
    return new ConsoleLogger(logger);
}
/** @private */
export class Subject {
    constructor() {
        this.observers = [];
    }
    next(item) {
        for (const observer of this.observers) {
            observer.next(item);
        }
    }
    error(err) {
        for (const observer of this.observers) {
            if (observer.error) {
                observer.error(err);
            }
        }
    }
    complete() {
        for (const observer of this.observers) {
            if (observer.complete) {
                observer.complete();
            }
        }
    }
    subscribe(observer) {
        this.observers.push(observer);
        return new SubjectSubscription(this, observer);
    }
}
/** @private */
export class SubjectSubscription {
    constructor(subject, observer) {
        this.subject = subject;
        this.observer = observer;
    }
    dispose() {
        const index = this.subject.observers.indexOf(this.observer);
        if (index > -1) {
            this.subject.observers.splice(index, 1);
        }
        if (this.subject.observers.length === 0 && this.subject.cancelCallback) {
            this.subject.cancelCallback().catch((_) => { });
        }
    }
}
/** @private */
export class ConsoleLogger {
    constructor(minimumLogLevel) {
        this.minimumLogLevel = minimumLogLevel;
    }
    log(logLevel, message) {
        if (logLevel >= this.minimumLogLevel) {
            switch (logLevel) {
                case LogLevel.Critical:
                case LogLevel.Error:
                    console.error(`[${new Date().toISOString()}] ${LogLevel[logLevel]}: ${message}`);
                    break;
                case LogLevel.Warning:
                    console.warn(`[${new Date().toISOString()}] ${LogLevel[logLevel]}: ${message}`);
                    break;
                case LogLevel.Information:
                    console.info(`[${new Date().toISOString()}] ${LogLevel[logLevel]}: ${message}`);
                    break;
                default:
                    // console.debug only goes to attached debuggers in Node, so we use console.log for Trace and Debug
                    console.log(`[${new Date().toISOString()}] ${LogLevel[logLevel]}: ${message}`);
                    break;
            }
        }
    }
}
