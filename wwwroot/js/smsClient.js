//All code related to SingalR JavaScript client.

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/smshub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.on("UpdateMessage", (msgId, progress) => {
    console.log("Updating message of id " + msgId);
    let messageUpdateProgressBar = document.getElementById("messageSendProgress-" + msgId);
    messageUpdateProgressBar.removeAttribute("aria-valuenow");
    messageUpdateProgressBar.setAttribute("aria-valuenow", progress * 25);
    messageUpdateProgressBar.setAttribute("style", "width: " + progress * 25 + "%");
});

connection.start().then(function () {
    console.log("Connected to smshub.");
}).catch(function (err) {
    return console.error(err.toString());
});

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