---
permalink: /docs/specklegsa_sending
title: "Sending data from SpeckleGSA"
excerpt: "Sending data from SpeckleGSA"
toc: false
---
Before you can send a stream, you first need to create/open a GSA file in the GSA tab so the `SpeckleGSAV2` application and GSA can communicate.

**Only files opened in the GSA tab are accessible by the SpeckleGSAV2 application.**

Sending streams can be done through the Sender tab. Choose which layers you want to send `Design` or `Both` (Both is the Design model and the Analysis model). Then click the `Send` button. Additional options for the sender is contained within the Settings tab. Once finished, the ID of the stream as well as the name will be displayed in the list view. Right clicking this entry will give two options: `Copy streamId` and `View Stream` to view the stream in your web browser.

![sending]({{site.baseurl}}/assets/images/quick_start/sending.gif)

## Sending results

To send analysis results from GSA, some settings must be modified within the result tab:
- Check the radio button in front of `Send model(s) with results`.
- Set the GSA result cases you want to send in the `Cases` field.
- Select which results you wish to export from the `Results` checkbox group.

![results]({{site.baseurl}}/assets/images/quick_start/results.gif)

Results can then be then be received in other clients and manipulated, for example in Grasshopper.