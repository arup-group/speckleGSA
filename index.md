---
permalink: /
title: SpeckleGSA
layout: archive

---

<img src="{{site.baseurl}}/assets/images/specklegsa-logo.png" style="float: right; padding: 0px 15px 0px 15px" width="50%" alt="SpeckleGSA logo">

SpeckleGSA is a Speckle client developed for [GSA 10](https://www.oasys-software.com/products/structural/gsa/) to allow the transfer of finite element models to and from SpeckleServers.

To install the latest version, download the [Speckle@Arup Account Manager Installer](https://github.com/arup-group/speckle-sharp/releases) in order to install the SpeckleGSA standalone application.

## Core SpeckleGSA Features:

* Automatically converts, packages, and sends GSA models to Speckle servers
* Allows for targeting of design or analysis layers
* Clone model streams for version control
	
##  What is Speckle?

[Speckle](https://speckle.works) empowers your design and construction data. Speckle is a safe and open source data platform that enables multi-disciplinary collaboration by providing a common mechanism for sharing data among different disciplines. It facilitates interoperability between the tools we use every day and integrates directly into them, like Revit, Rhino, Grasshopper, GSA, Civil3D, Microstation and more!

## How it Works

Visit the [Speckle website](https://speckle.systems/) for more information.<br>
Or go directly to the [Speckle documentation pages](https://speckle.guide/).

## Recent news

{% for post in site.categories.news limit:4  %}
<h3><a href="{{ site.baseurl }}{{ post.url }}">{{ post.title }}</a></h3>

{{ post.excerpt }}

{% endfor %}