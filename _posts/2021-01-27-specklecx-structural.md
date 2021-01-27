---
title: Speckle Community Extensions (CX) clients v1.0.8
author: David de Koning
date: 2021-01-27
categories: 
    - news
---

It is our pleasure to announce [Version 1.0.8 of the Speckle CX client installer](https://github.com/arup-group/SpeckleInstaller/releases/tag/1.0.8.33337). This update includes the following

Structural Suite:
* data received into GSA should have a consistent and rational order
* there is a new Structural1DPropertyExplicit data type, which allows you to specify a property by it's Engineering Beam Theory parameters

Grasshopper client:
* The Sender and Receiver components now show what server they are connected to
* The Sender component allows you to directly add the stream to a Project
* Fixed a localhost bug that prevented some people from adding accounts (the dreaded port 5050 bug!)
* Added the first iteration of Rhino.Compute functionality (which most users won't use, so don't worry about it :) )

All Arup staff should use this all-in-one installer and everyone else is welcome to use it as well!
{: .notice--success}

You may get a warning screen from Windows Defender due to the executable being downloaded from the internet. It is safe to ignore this message and continue installation (and yes, we are working on removing the warning!).
{: .notice--warning}

## Thanks

Thanks to Nic Burgers and Stam Psarras for this work!
