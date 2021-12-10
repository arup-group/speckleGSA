---
permalink: /docs/specklegsa_receiving
title: "Receiving data into SpeckleGSA"
excerpt: "Receiving data into SpeckleGSA"
toc: false
---
Before you can receive a stream, you first need to create/open a GSA file in the GSA tab so the `GSA connector for Speckle` application and GSA can communicate.

**Only files opened in the GSA tab are accessible by the GSA connector for Speckle application.**

![receiving01 - open or new file]({{site.baseurl}}/assets/images/quick_start/receiving01.png)

Receiving streams can be done through the Receiver tab. Add the stream ID of the stream you wish to receive using the `Add Receiver` button. Next, click on  the `Receive` button to start receiving.

![receiving02 - receive]({{site.baseurl}}/assets/images/quick_start/receiving02.png)

Within the Settings tab, there are two options to change how the sender operates.
- The `Coincident Node Allowence` specifies the distance of 2 nodes in order to coincide them.
- The `Distance units` specifies in which units this distance is defined.

![receiving03 - settings]({{site.baseurl}}/assets/images/quick_start/receiving03.png)

![receiving]({{site.baseurl}}/assets/images/quick_start/receiving.gif)