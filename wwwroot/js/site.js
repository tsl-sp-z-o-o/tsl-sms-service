//Main file for editing DOM of the site.
function textAreaAdjust(o) {
  o.style.height = "1px";
  o.style.height = (25+o.scrollHeight)+"px";
}


document.getElementById("cancel-sms-messages").addEventListener("click", function () {
    connection.invoke("Cancel").catch(function (err) {
        return console.error(err.toString());
    });
});