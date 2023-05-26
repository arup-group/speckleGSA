---
permalink: /docs/specklegsa_gsa_lists
title: "Using GSA lists with Speckle"
excerpt: "GSA lists with Speckle"
toc: false
---
You can now use GSA lists in Speckle! Keep reading to understand the level of support, limitations and for examples how to use GSA lists.

## GSA Lists in GSA

### Sending Lists from GSA to Speckle

Speckle currently supports sending GSA lists to Speckle for the following list types:
- Nodes
- Members
- Elements

Conversion is supported for lists with all variations of definitions (using prefixes or shorthand) as defined in [Oasys GSA's docs](https://docs.oasys-software.com/structural/gsa/references/listsandembeddedlists.html#list-definition), **except** the following:
- Named lists: support using named lists as a sub-list to define another list is currently limited to using only one list name - e.g. `"List 1"` without additional list names or indices. Instead, if you wish to define a list by a more complex arrangement of sub-lists (and/or indices), we recommend providing the **list number** as per GSA's docs - e.g. using a definition of `#1 #2` instead of `"List one name" "List two name"`. *Note, a similar limitation applies to using assemblies or grid surfaces to define lists.*
- Asterisk shorthand: in GSA, it is valid to provide a list definition using the asterisk shorthand, e.g. `2 to *`, which will include all objects from 2 to the last index. This shorthand is not currently supported in conversion to Speckle.

GSA list objects sent from GSA to Speckle will contain references to the ids of the objects it contains. You can view these references in the `definitionRefs` property of the GSA list object in Speckle.

### Receiving Lists in GSA from Speckle

Speckle also only provides support for the list types documented above on receiving to GSA. On receiving, GSA lists will be created using indices only to define list objects, e.g. `1 2 3 4 5`, and conversion will not use any prefixes or shorthand notation.

*Note, always check the report log on the SpeckleGSA UI on sending and receiving lists for additional information logged during the conversion of GSA lists.*

## GSA Lists in Grasshopper
### Using GSA Lists in Grasshopper
GSA list components can only accept Speckle objects of the type selected (Nodes/Elements/Members) as a definition, e.g.:
- Node: definition accepts Speckle objects of type Node or GSANode
- Element: definition accepts Speckle objects of type Element1D or GSAElement1D
- Member: definition accepts Speckle objects of type GSAMember1D

An error will be displayed on the component if objects with a different type to the specified value are passed in. Additionally, GSA lists definitions will not accept other GSA list objects as an input to the definition parameter.

![list-use-gh]({{site.baseurl}}/assets/images/gsa_lists/list-use-gh.png)

### Sending GSA Lists from Grasshopper to Speckle

Current implementation of GSA lists only provides support for passing them as inputs to a `Model` component or a `Send` component directly. Support for passing GSA list components into other components (e.g. other GSA list or LoadFace components) as you might typically in GSA is not currently supported. GSA list components are for defining list objects to be created in GSA only.

### Receiving GSA Lists into Grasshopper from Speckle

GSA lists received into Grasshopper will be accessible through the `General Data` property of the `Model` object. GSA lists received from Speckle but originally from GSA will contain a list of references to object ids contained within the list in the `definitionRefs` property.

![gh-list-receive]({{site.baseurl}}/assets/images/gsa_lists/gh-list-receive.png)