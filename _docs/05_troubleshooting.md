---
permalink: /docs/troubleshooting
title: "Troubleshooting SpeckleGSA"
excerpt: "Troubleshooting SpeckleGSA"
toc: false
---

Here are some tips for troubleshooting SpeckleGSA and the StructuralKit. We are working to improve the software so that these issues do not appear, but in the meantime, here are some tips and workarounds!

## I am missing StructuralKit elements!

If you only have a few StructuralKit elements available in the SchemaBuilderComponent, like so:

![StructuralKit Elements missing from SchemaBuilderComponents]({{site.baseurl}}/assets/images/missing-structuralkit-elements.png)

you can type `GrasshopperDeveloperSettings` in the Rhino command window and uncheck the `Memory load *.GHA assemblies using COFF byte arrays` checkbox. You may have to restart Rhino after changing this setting.

![Grasshopper Developer Settings]({{site.baseurl}}/assets/images/grasshopper-dev-settings.png)

## I cannot log into my speckle server account

You may receive the following error when trying to log into your Speckle Server account:

![The I/O operation has been aborted because of either a thread exit or an application request.]({{site.baseurl}}/assets/images/speckle-io-abort-error.png)

Try restarting Grasshopper **and Rhino**, since this will clear previous login attemps that may be blocking the current attempt.

## I updated Speckle Grasshopper and now my data is not being sent

When you upgrade the Speckle Grasshopper plugin, sometimes the sender does not send anymore. While we are working to fix this, replacing the sender with a new one will get your data flowing again (yes, we know, it also creates a new stream :( ).

## General Grasshopper problems

If you are having problems with the Grasshopper plugin, try the following:

1. install [MetaHopper](https://www.food4rhino.com/app/metahopper)
2. open the [this GH file]({{site.baseurl}}/assets/sample_files/gh/component_paths.zip)

The grasshopper file will show you which version of the Rhino/Grasshopper plugin Grasshopper is using, and 
show a list of various Speckle plugins that are installed on your system. If you have more than one installed,
delete the older ones (or delete them all and reinstall...)
