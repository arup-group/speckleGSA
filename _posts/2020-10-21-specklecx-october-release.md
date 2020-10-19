---
title: Speckle Community Extensions (CX) October Update
author: Hugh Groves
date: 2020-10-21
categories: 
    - news
---

We have a collection of updates to share with you today:

* Documentation
* Grasshopper
* Installer
* GSA

## Documentation

This site has been updated with a much more detailed guide for creating a structural model in Grasshopper and sending it to GSA. [You can follow along here](/docs/gh_sending).

## Grasshopper

We have released an alternative implementation of the Speckle Grasshopper plugin that we are calling Speckle Grasshopper Community Extensions or SpeckleGrasshopper-cx. [You can find the latest version on the GitHub releases page here.](https://github.com/arup-group/SpeckleRhino/releases)

SpeckleGrasshopper-cx is a refinement of the main SpeckleGrasshopper plugin based upon user feedback within Arup. It specifically smooths structural model creation.

Highlights include:

* The naming and grouping of the components have been simplified. Rarely used components have been hidden from the main task bar.
* Property references are now easier to assign, simply wire in the appropriate Speckle object to an input ending in 'Ref' and the application id of the object will be automatically extracted from the Speckle object and assigned appropriately.
* Querying properties within the properties dictionary (where most structural data resides) has been unified with querying top-level fields.

The documentation on this site has been written using the SpeckleGrasshopper-cx components.

## All-in-one installer

We have released an all-in-one installer for all of the SpeckleGSA and Speckle-CX suite. [You can download it from here](https://github.com/arup-group/SpeckleInstaller/releases).

All Arup staff should use the all-in-one installer.
{: .notice-success}

## GSA

SpeckleGSA has been updated. This release mostly contains bug fixes and performance improvements.

If you don't want to use the all-in-one installer above you can find the [latest version of SpeckleGSA here](https://github.com/arup-group/speckleGSA/releases).

## Thanks

Thanks to a big group for contributing to this one:

* Nic Burgers
* Stamatios Psarras
* Jenessa Man
* Peter Grainger
* David de Koning
* Megan Karbowski
* Hugh Groves