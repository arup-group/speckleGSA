---
permalink: /
title: SpeckleGSA
layout: archive

---

<img src="/assets/images/specklegsa-logo.png" style="float: right; padding: 15px" width="50%" alt="SpeckleGSA logo">

SpeckleGSA is a Speckle client developed for [GSA 10](https://www.oasys-software.com/products/structural/gsa/) to allow the transfer of finite element models to and from SpeckleServers.

To install the latest version, download the [Speckle Structural Suite Installer](https://github.com/arup-group/specklestructuralsuite-installer/releases).

## Core SpeckleGSA Features:

* Automatically converts, packages, and sends GSA models to Speckle servers
* Allows for targeting of design or analysis layers
* Clone model streams for version control
* Built on top of [SpeckleStructures](https://github.com/speckleworks/SpeckleStructural/)!
	
## Recent news


{% for post in site.categories.news limit:4  %}
<h3><a href="{{ site.baseurl }}{{ post.url }}">{{ post.title }}</a></h3>

{{ post.excerpt }}

{% endfor %}


##  What is Speckle?

[Speckle](https://speckle.works) is an open digital infrastructure for designing, making and operating the built environment. It is a platform which connects various Architecture, Engineering, and Construction (AEC) tools together to foster inter-disciplinary communication and increase the ease with which design collaboration occurs.

Some of you may be familiar with similar digital design collaboration and interoperability platforms like Flux or Konstru, but what makes Speckle different is its extensible, open-source, and vendor-neutral nature. This means that Speckle is designed to accommodate extensions to its ecosystem and to support interoperability with any data model or AEC tool. In other words, Speckle development and growth is driven wholly by the users, and Speckle is owned by the AEC community itself. With Speckle-based workflows, users avoid proprietary lock-in, avoid subscription-based costs, and maintain complete ownership and control of their design data. 

## How it Works

[Speckle](https://speckle.works) is a web-based platform. It works by storing your design data in an environment which can be accessed and manipulated through the web; this environment is called a SpeckleServer. The pieces of data stored on a SpeckleServer are called SpeckleObjects. While data of any type can be stored as a SpeckleObject, geometry information generally forms the basis of SpeckleObject data. Non-geometric information and design metadata can be attached to a SpeckleObject with property tags.

SpeckleObjects are pieces of data which are stored in a SpeckleServer. They could be points, lines, or even simply words or numbers. You can attach data such as member thickness, colour, materials, etc.

SpeckleObjects can be grouped and organized together into SpeckleStreams. The Speckle ecosystem provides a useful set of built-in features for sharing SpeckleStream data in an online viewer called SpeckleViewer and performing various versioning and comparison operations on different SpeckleStreams. For example, a SpeckleStream could be created to represent a specific design option for a project. This design option could be communicated to and shared with the larger project team in SpeckleViewer. This design option could then be overlaid against and compared with a previous design option to facilitate design iteration and evaluation.

While communication with a SpeckleServer is performed through the web, communication between a specific AEC tool and the larger Speckle ecosystem is facilitated through a Speckle Client (e.g., communication between Rhino and Speckle is facilitated through the SpeckleRhino client). A Speckle Client performs this communication using sender and receiver components. These components are responsible for converting, packaging and transferring data to and from the server.