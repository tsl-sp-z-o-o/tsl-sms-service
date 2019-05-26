//All code related to SingalR JavaScript client.
"use strict";


const connection = new signalR.HubConnectionBuilder()
    .withUrl("/smshub")
    .configureLogging(signalR.LogLevel.Debug)
    .build();

async function start() {
    try {
        await connection.start();
        console.log("connected");
    } catch (err) {
        //console.log(err);
        setTimeout(() => start(), 50000);
    }
};


connection.onclose(async () => {
    await start();
});

connection.on("StartProgressCheck", (delay) => {
    StartCheckProgress(delay);
});

connection.on("ReceiveMessagesStatus", function(sentCount){
    let statusControl = document.getElementById("status-control");
    statusControl.innerHTML = sentCount;
});

function StartCheckProgress(delay) {
    window.setInterval(function () {
        connection.invoke("GetMessagesStatusUpdate").catch(function (err) {
            console.error(err.toString());
        });
    }, delay);
}


connection.start().then(function () {
    console.log("Connected to smshub.");
}).catch(function (err) {
    return console.error(err.toString());
});
