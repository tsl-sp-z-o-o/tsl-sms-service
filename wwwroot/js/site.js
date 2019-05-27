//Main file for editing DOM of the site.
function textAreaAdjust(o) {
  o.style.height = "1px";
  o.style.height = (25+o.scrollHeight)+"px";
}

$('document').ready(function () {
    if (document.getElementById('cancel-sms-messages')) {
        let cancelSmsMessages = document.getElementById("cancel-sms-messages");
        cancelSmsMessages.addEventListener("click", function () {
            connection.invoke("Cancel").catch(function (err) {
                return console.error(err.toString());
            });
        });
    }

    if (document.getElementById('select-all-button')) {
        let selectAllMessages = document.getElementById("select-all-button");
        selectAllMessages.addEventListener("click", function () {
            let messageList = document.getElementById("messages-list");
            for (var i = 0; i < messageList.options.length; i++) {
                messageList.options[i].selected = true;
                
            }
        });
    }
});
