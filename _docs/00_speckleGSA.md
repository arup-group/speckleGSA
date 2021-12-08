---
permalink: /docs/
title: "SpeckleGSA"
excerpt: "SpeckleGSA"
toc: true
---

Download the [latest Speckle Structural Suite Installer](https://github.com/arup-group/specklestructuralsuite-installer/releases) from the GitHub releases page.

## Opening the plugin
SpeckleGSA runs as its own standalone application. After you've installed Speckle and the GSA plugin, the client should be added to your start menu as `SpeckleGSAV2`.

## Interface
The plugin is separated into tabs:
- **Server**: login or create an account in their Speckle server of choice
- **GSA**: create a new or open an existing GSA file
- **Sender**: sends the GSA model
- **Receiver**: receive streams into the GSA file
- **Settings**: contains all settings

## Creating an account or logging in
The SpeckleGSA client should automatically authenticate with the Speckle server thanks to the Speckle@Arup AccountManager. You can change your account and server settings by clicking by `Manage Accounts` on the top right, which will open the Speckle@Arup AccountManager. Within Arup we host our own Speckle servers and there is a server available for all our colleagues to use `https://v2.speckle.arup.com/`. For large projects a dedicated server can be requested.

![login]({{site.baseurl}}/assets/images/quick_start/login.png)

## Receiving and sending

* [Receiving data into SpeckleGSA](specklegsa_receiving)
* [Sending data from SpeckleGSA](specklegsa_sending)