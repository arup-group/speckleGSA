---
permalink: /docs/speckleGSA_sending
title: "Sending data from SpeckleGSA"
excerpt: "Sending data from SpeckleGSA"
toc: false
---

Once logged in, you can start sending GSA models. First, either create or open a GSA file from the GSA tab. **Only files opened through this menu are accessible by the client.** Next, switch to the sender tab and click on the <i class="fa fa-play-circle"></i> button to start sending. Once finished, the ID of the stream as well as the name will be displayed in the list view. Right clicking this entry will give various useful options, such as viewing the model in the [online viewer](/docs/basics/viewer), copying the stream ID, or cloning the stream.

![sending]({{site.baseurl}}/assets/images/quick_start/sending.gif)

You will need to pause the sender by clicking the <i class="fa fa-pause-circle"></i> button before receiving streams or changing any settings.
{: .notice-primary}

Within the sender tab, there are two toggles to change how the sender operates.
- The `Layer to Stream` toggle allows you to send either the `Design` or `Analysis` layer of GSA.
- The `Streaming Method` toggle changes whether the plugin should watch GSA and update the stream as changes are made or whether the stream should be updated whenever the <i class="fa fa-play-circle"></i> button is selected.

Additional options for the sender is contained within the settings tab.

## Sending results

To send analysis results from GSA, some settings must be modified within the result tab:
- Set the GSA result cases you want to send in the `cases` field. The cases should be delimited by a new line.
- Select which results you wish to export from the `result to send` checkbox group

Results can only be sent from the <b>analytical</b> layer in <b>single</b> streaming mode.
{: .notice--warning}

By default, the result are embedded within the object itself. If you wish to send only results, unselect `embed results in objects` and select `send only results`.

![results]({{site.baseurl}}/assets/images/quick_start/results.gif)

Results can then be then be received in other clients and manipulated, for example [in Grasshopper](gh_receiving).