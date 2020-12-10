---
title: Speckle Community Extensions (CX) clients v1.0.7
author: David de Koning
date: 2020-12-10
categories: 
    - news
---

It is our pleasure to announce an [update to the Speckle CX clients](https://github.com/arup-group/SpeckleInstaller/releases/tag/1.0.7.64805). This update is focused on the structural suite, with the following bug fixes and improvements:

* The outer edge of 2D meshes (elements and members) is now correctly identified and the vertex list is consolidated to remove duplicates.
* 1D Loads now include a LoadPlaneRef
* Assemblies now inlude a StoreyRef
* Bug fixes for 0D (node) and 1D loads and 2D load panels
* Bug fix for property references in polylines
* Loads are no longer duplicated every time you receive a stream
* There is now a shortcut to SpeckleGSA in the Oasys start menu folder

These improvements are available in the [all-in-one installer for all of the Speckle desktop clients](https://github.com/arup-group/SpeckleInstaller/releases/tag/1.0.7.64805).

All Arup staff should use this all-in-one installer. Anyone else is welcome to use it as well!
{: .notice--success}

You may get a warning screen from Windows Defender due to the executable being downloaded from the internet. It is safe to ignore this message and continue installation (and yes, we are working on removing the warning!).
{: .notice--warning}

## Thanks

Thanks to Nic Burgers for tracking down and fixing these bugs, and Peter Grainger for updating the installer!
