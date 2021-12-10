---
permalink: /docs/specklegsa_sending
title: "Sending data from SpeckleGSA"
excerpt: "Sending data from SpeckleGSA"
toc: false
---
Before you can send a stream, you first need to create/open a GSA file in the GSA tab so the `GSA connector for Speckle` application and GSA can communicate.

**Only files opened in the GSA tab are accessible by the GSA connector for Speckle application.**

![sending01 - open or new file]({{site.baseurl}}/assets/images/quick_start/sending01.png)

Sending streams can be done through the Sender tab. Choose which layers you want to send `Design` or `Both` (Both includes the Design model and the Analysis model). Then click the `Send` button.

![sending02 - send]({{site.baseurl}}/assets/images/quick_start/sending02.png)

Additional options for the sender are contained within the Settings tab. Once finished, the ID of the stream as well as the name will be displayed in the list view. Right clicking this entry will give two options: `Copy streamId` and `View Stream` to view the stream in your web browser.

![sending03 - view stream]({{site.baseurl}}/assets/images/quick_start/sending03.png)

![sending]({{site.baseurl}}/assets/images/quick_start/sending.gif)


## Sending results

To send analysis results from GSA, some settings must be modified within the Settings tab:
- Check the radio button in front of `Send model(s) with results`.
- Set the GSA result cases you want to send in the `Cases` field.
- Select which results you wish to export from the `Results` checkbox group.

Results can then be received in other clients and manipulated, for example in Grasshopper.

![sending04 - settings]({{site.baseurl}}/assets/images/quick_start/sending04.png)

![results]({{site.baseurl}}/assets/images/quick_start/results.gif)