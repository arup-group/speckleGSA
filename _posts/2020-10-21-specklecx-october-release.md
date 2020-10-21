---
title: Speckle Community Extensions (CX) October Update
author: Hugh Groves
date: 2020-10-21
categories: 
    - news
---

It is our pleasure to announce a set of Speckle updates that simplify the construction of structural analysis models with Speckle:

* Speckle structural documentation
* A spruced-up Grasshopper plugin
* A brand new Speckle Installer
* SpeckleGSA v0.11.1.2

## Speckle structural documentation

The SpeckleGSA documentation has been updated with a detailed guide to creating structural models in Grasshopper and sending them to GSA. [You can follow along here]({{site.baseurl}}/docs/gh_sending).

The documentation on this site has been written using the <i class="fas fa-arrow-down"></i><i class="fas fa-arrow-down"></i><i class="fas fa-arrow-down"></i>

## Spruced-up Grasshopper plugin

Since the main speckle plugins [are not being actively developed](https://speckle.systems/blog/insider-speckle2), we have released our progression of the Speckle Grasshopper plugin called Speckle Grasshopper Community Extensions or SpeckleGrasshopper-cx. Our work is open-source, and you can find [all the code here](https://github.com/arup-group/SpeckleRhino).

SpeckleGrasshopper-cx is a refinement of the main SpeckleGrasshopper plugin based upon user feedback within Arup. In particular, is simplifies your life when creating structural models.

Highlights include:

* The naming and grouping of the components have been simplified. Rarely used components have been hidden from the main task bar.
* Property references are now easier to assign, simply wire in the appropriate Speckle object to an input ending in 'Ref' and the application id of the object will be automatically extracted from the Speckle object and assigned appropriately.
* Querying properties within the properties dictionary (where most structural data resides) has been unified with querying top-level fields.

If you have previously given up building a structural model in GH with the speckle plugin, now is the time to give it another shot! The new components and the comprehensive documentation make the process much easier! 

To get this new plugin, check out the <i class="fas fa-arrow-down"></i><i class="fas fa-arrow-down"></i><i class="fas fa-arrow-down"></i>

## Brand new Speckle Installer

We have released an all-in-one installer for all of the Speckle desktop clients. It includes SpeckleGSA and the SpeckleGrasshopper-cx plugin, as well as all of the main Speckle clients and kits. [You can download it from here](https://github.com/arup-group/SpeckleInstaller/releases/latest).

All Arup staff should use the all-in-one installer. Anyone else is welcome to use it as well!
{: .notice--success}

You may get a warning screen from Windows Defender due to the executable being downloaded from the internet. It is safe to ignore this message and continue installation.
{: .notice--warning}

## SpeckleGSA

SpeckleGSA has been updated:

* Automatically logged in using your default account
* When streams are sent from SpeckleGSA they will now appear in just two layers: model and results
* Sending now happens on the analysis layer by default
* Streams can be removed
* Load cases can now be specified in the GSA-style string like "A2, A3 to A7"
* Native IDs and properties are sent along with elements, nodes, properties and materials
* Various bugfixes

If you don't want to use the all-in-one installer above you can find the [latest version of SpeckleGSA here](https://github.com/arup-group/speckleGSA/releases/latest).

## Thanks

Thanks to a big group for contributing to this one:

* Nic Burgers
* Stamatios Psarras
* Jenessa Man
* Peter Grainger
* David de Koning
* Megan Karbowski
* Hugh Groves