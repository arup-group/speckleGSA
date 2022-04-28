---
permalink: /docs/specklegsa_receiving
title: "Receiving data into SpeckleGSA"
excerpt: "Receiving data into SpeckleGSA"
toc: false
---
Before you can receive a stream, you first need to create/open a GSA file in the GSA tab so the `GSA connector for Speckle` application and GSA can communicate.

**Only files opened in the GSA tab are accessible by the GSA connector for Speckle application.**

![new-or-open-gsa]({{site.baseurl}}/assets/images/quick_start/new-or-open-gsa.png)

Select the desired stream from the list of streams.

![select-stream-to-receive]({{site.baseurl}}/assets/images/quick_start/select-stream-to-receive.png)

Select `receive` and expand the options.

![select-receive]({{site.baseurl}}/assets/images/quick_start/select-receive.png)

Within the expanded options, there are three options to modify how the sender operates.
- The `Coincident Node Allowence` specifies the distance of 2 nodes in order to coincide them.
- The `Distance units` specifies in which units this distance is defined.
- The `Advanced Settings` allows the user to specify a speckle stream that the conversion will use to map catalogue sections [ (Section Mapping)](06_section_mapping.md).

![set-units - settings]({{site.baseurl}}/assets/images/quick_start/set-units.png)

Hit the receive button to receive the data into GSA!
