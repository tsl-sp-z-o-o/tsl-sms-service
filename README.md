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

Docs for admins are accessible [here](https://github.com/tsl-sp-z-o-o/tsl-sms-service/blob/development/wwwroot/files/sysadmin-doc.rtf) as an RTF file.

**Content**

1. Stack

2. Deployment notes

3. Software notes

**1. Stack**

Technology stack used consists of:

- NET Core 2.2,
- Entity Framework with Npgsql driver,
- PostgreSQL 11 RDBMS,
- nginx,
- gammu 1.40,
- python-gammu 2.12,
- python 3.7 x64.

Windows Server 2016 should have at least 6GBs of RAM and 2 vCPUs, however it is recommended to give it more, like 4 vCPUs.

**2. Deployment notes**

Paths:

**Site: C:\ServerFolders\Sites\tsl-web-service**

**App configuration: C:\ServerFolders\Company\tsl**

**nginx: G:\nginx\**

Application uses configuration stored in its root directory in file: **appsettings.json**.

From there, you can reconfigure it, but any reconfiguration is at on your own risk.

**Note: Remember to change database settings in this file.**

There is a .bat file in configuration directory, however it shouldn&#39;t be used, it is just in case.

Application starts upon Kestrel WWW Server on the following URLs: [https://localhost:5001](https://localhost:5001) and [http://localhost:5000](http://localhost:5000).

For now, nginx is configured as a reversed proxy to the second URL.

Each, an app and nginx are started automatically at the system&#39;s startup.

To start them, restart, stop etc just use nssm which is an better service manager.
It is accessible from cmd.exe or powershell.exe, for more info type: nssm --help.

NSSM services:

- SmsService - core app service,
- nginx - nginx service,
- SmsServiceLimitedMem - details below in part 3.

NSSM is placed under the following path **: G:\nssm\**

There is NO SSL certificate configured at the moment.

**Please generate the SSL certficate and reconfigure nginx then restart its service.**

All drivers for GSM modem should be installed. After doing that, the only thing is to correctly link USB device on physical host to a VM. Drivers should be installed on each, a host and a VM.

**3. Software notes**

General

There are some things about SMS Service.

The first one: it uses gammu and all python scripts or other configured modules via standard system Process API.

All modules are configured in: modules.json file, however adding anything won&#39;t give any effect as the software is not prepared for running any kind of modules.

There can be severe errors about connecting to to COM ports. In such a case, there is a possibility to reconfigure it &quot;on-the-go&quot;, but if this does not work try restarting whole service.

Logging

All critical, information and warning logs are stored in system log:

Application name: SMSService,

SourceName: SMSServiceSource.

**Event ids and their meanings:**

0 - Common Event,

1 - Invalid Operation Event,

2 - Invalid Configuration Event,

3 - Hardware Event,

4 - Database Event

Resources usage

In case of too high CPU or RAM usage, I have left special program to limit it: procgov and a special service &quot;SmsServiceLimitedMem&quot; for this is configured in nssm with ram limitation to 0.5GB.
