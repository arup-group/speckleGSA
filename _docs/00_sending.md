---
permalink: /docs/specklegsa_sending
title: "Sending data from SpeckleGSA"
excerpt: "Sending data from SpeckleGSA"
toc: false
---
Before you can send a stream, you first need to create/open a GSA file in SpeckleGSA so the `GSA connector for Speckle` application and GSA can communicate.

**Only files opened in the GSA tab are accessible by the GSA connector for Speckle application.**

![new-or-open-gsa]({{site.baseurl}}/assets/images/quick_start/new-or-open-gsa.png)

Select the desired stream from the list of streams.

![select-stream-to-receive]({{site.baseurl}}/assets/images/quick_start/select-stream-to-receive.png)

Select `send` and expand the options.

![select-send - send]({{site.baseurl}}/assets/images/quick_start/select-send.png)

Additional options for the sender are now available. The user can modify the following behaviour of the sender:
- Speckle branch to send data to
- What to send (see [Filtering](#filtering))
- Layer to send (see GSA documentation for more info on layers)
- `Advanced Settings` allows the user to specify a speckle stream that the conversion will use to map catalogue sections [ (Section Mapping)](06_section_mapping.md).

![send-options](/assets/images/quick_start/send-options.png)

Press the `send button` to send the data from GSA to speckle. You can track the logs of sent/received streams easily in the desktop UI, as well as selecting the shortcut to view the stream in the Speckle web viewer.

![send-log]({{site.baseurl}}/assets/images/quick_start/send-log.png)

## Filtering

SpeckleGSA currently supports two options for filtering:
- Send everything (on selected layers)
- Send by list

Sending by list utilizes native GSA lists to group objects within the model. All lists defined in the GSA model will be visible for selection in the SpeckleGSA connector. 

The connector currently supports the following list types from GSA:
- Member
- Element
- Node
- Case

![gsa to speckle list filters]({{site.baseurl}}/assets/images/quick_start/GsaToSpeckleLists.gif)

## Sending results

Currently not available on DesktopUI v2. 

*Coming soon.*
