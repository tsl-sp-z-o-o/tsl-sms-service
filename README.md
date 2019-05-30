# tsl-sms-service
TSL SMS Web Service

# What is this?

This is a simple web application written in ASP.NET Core 2.2/3.0 Preview 5 using C# 7.0. And here is "stable" version of software, for unstable, but the newest version check out "development" branch.

# How does it work?

It uses 3rd party software: [gammu](https://wammu.eu/gammu/) to send SMS via GSM modem using AT commands.
All user data and SMS data imported from CSV files are stored in Postgresql 11 database. 
Application has been written, built and tested on Windows 10 x64. It is supposed to work on Windows Server 2012 or 2016(what is highly recommended).

# Documentation

## Sysadmin documentation

Docs for admins are accessible [here](https://github.com/tsl-sp-z-o-o/tsl-sms-service/blob/development/wwwroot/files/sysadmin-doc.rtf).
