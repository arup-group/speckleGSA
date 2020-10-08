---
permalink: /docs/troubleshooting
title: "Troubleshooting SpeckleGSA"
excerpt: "Troubleshooting SpeckleGSA"
toc: false
---

Here are some tips for troubleshooting SpeckleGSA.

## Grasshopper components not behaving

If the SchemaBuilderComponent is not showing all the SpeckleStructural types, try the following:

1. install [MetaHopper](https://www.food4rhino.com/app/metahopper)
2. open the [this GH file]({{site.baseurl}}/assets/sample_files/gh/component_paths.zip)

The grasshopper file will show you which version of the Rhino/Grasshopper plugin Grasshopper is using, and 
show a list of various Speckle plugins that are installed on your system. If you have more than one installed,
delete the older ones (or delete them all and reinstall...)