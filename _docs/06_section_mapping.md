---
permalink: /docs/specklegsa_section_mapping
title: "Section Mapping with Speckle"
excerpt: "Mapping catalogue sections between software with Speckle"
toc: false
---

# Structural Section Mapping

The interoperability between the GSA and Revit connectors supports section mapping between catalogue sections and GRS revit families. Mapping for additional software can be implemented by users (see below).

For specific instructions on implementing section mapping on sending/receiving, see documentation for specific connectors.

- [GSA](00_receiving.md)
- [Revit](02_revit.md)

## Default Mapping

Present implementation only supports use of the default mapping stream (https://v2.speckle.arup.com/streams/e53a0242be).


To best view/modify the mapping data, the user will need to pull the current mapping data from the stream URL into Excel. At this time, the SpeckleExcel connector does not allow streams to be received by URL. As a workaround, receive the stream into grasshopper and then send this data to a new stream accessible from your account. You will then be able to receive this data into Excel. Any updated data should be sent to original default mapping stream collaborators to include in the stream that is currently used by connectors.<br>

To update/append to section mappings, the user should maintain heading naming conventions and maintain the branch structure used in the default mappings stream, i.e. a separate branch for mappings and section data.

*Note: Future functionality will be added to allow custom streams to be used for section mapping, allowing users to specify their own mappings if preferred. This would alleviate the need to send updated mappings to default mapping stream collaborators to update.*

