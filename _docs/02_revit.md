---
permalink: /docs/revit
title: "Revit"
excerpt: "Revit"
toc: false
---

Stay tuned for updates.
{: .notice--danger}

<i class="fa fa-graduation-cap"></i>For Speckle-Revit documentation [refer to the main Speckle documentation site](https://speckle.systems/tag/revit/).
{: .notice--primary}

Using the Revit plugin and the GSA plugin, you can send the Revit Analytical Model and receive it in GSA. To do so, add the analytical model elements you wish to send to a sender in the Revit plugin and receive as usual in GSA.

![revittogsa](/assets/images/user_docs/revittogsa.gif)

<br>

## Section Mapping
This section covers the steps to ensure structural section mapping is implemented on sending/receiving of elements in SpeckleRevit. For further background information on the mapping data, see [Section Mapping](06_section_mapping.md).

### 01
In Revit, select the Speckle tab from the toolbar and open the SpeckleRevit connector.

![Open connector](/assets/images/revit/00_open-revit-connector.png)

### 02
Select the desired stream to send to/receive from.

![Select stream](/assets/images/revit/01_select-stream.png)

### 03
Select the desired stream to send to/receive from.

![Expand advanced options](/assets/images/revit/02_expand-options.png)

### 04
From the drop-down box, select Section Mapping.

![Select section mapping](/assets/images/revit/03_drop-down-selection.png)

### 05
From the second drop-down box, select the desired stream that contains the section mapping data.

![Select section mapping](/assets/images/revit/04_drop-down-stream-selection.png)

### 06
Select save and proceed to send/receive data as normal!
