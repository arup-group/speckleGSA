---
permalink: /docs/
title: "SpeckleGSA"
excerpt: "SpeckleGSA"
toc: true
---

Download the [latest Speckle Structural Suite Installer](https://github.com/arup-group/specklestructuralsuite-installer/releases) from the GitHub releases page.

## Opening the plugin

SpeckleGSA runs as its own standalone application. After you've installed Speckle and the GSA plugin, the client should be added to your start menu as `SpeckleGSA`.

## Interface
The plugin is separated into tabs:
- **Server**: login or create an account in their Speckle server of choice
- **GSA**: create or open a new GSA file
- **Sender**: sends the GSA model
- **Receiver**: receive streams into the GSA file
- **Settings**: contains all settings

## Creating an account or logging in
As with most other clients, you will need an account on the Speckle server of your choice. This can be done by clicking `Login` from the server tab. If you do not have a Speckle server available and just want to test out Speckle, you can use `https://hestia.speckle.works/api`

Do be advised that this `https://hestia.speckle.works/api` is a test server - use at your own risk!
{: .notice--danger}

![login]({{site.baseurl}}/assets/images/quick_start/login.png)

## Receiving and sending

* [Receiving data into SpeckleGSA](specklegsa_receiving)
* [Sending data from SpeckleGSA](specklegsa_sending)