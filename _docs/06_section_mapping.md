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

This stream can be pulled into Excel to view/modify the data. To modify the stream, the updated data should be sent to stream collaborators.<br>

To update/append to section mappings, the user should maintain heading naming conventions and maintain the branch structure used in the default mappings stream, i.e. a separate branch for mappings and section data.

*Note: Future functionality will be updated to allow custom streams to be used for section mapping, allowing users to specify their own mappings if preferred.*

