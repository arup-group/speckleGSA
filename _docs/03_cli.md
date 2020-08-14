---
permalink: /docs/cli
title: "Command line interface (CLI)"
excerpt: "Command line interface (CLI)"
toc: false
---

The GSA plugin has a CLI interface to send and receive GSA models without the use of the interface. To use it, you will need to obtain the `token` associated with your account, which can be found from the *Profile* page of the Speckle admin app. For detailed usage help, execute the `SpeckleGSAUI -h` command.

Sample sending command:
```
SpeckleGSAUI sender --server "https://hestia.speckle.works/api" --email mishael.nuh@arup.com --token "JWT XXXXXX" --file "C:\Speckle\Speckle Demos\GSA Demo.gwb" --layer analysis --result "2D Element Derived Force" --resultCases A1
```

Sample receiving command:
```
SpeckleGSAUI receiver --server "https://hestia.speckle.works/api" --email mishael.nuh@arup.com --token "JWT XXXXXX" "C:\Users\Mishael.Nuh\Desktop\Received File.gwb" --layer design --nodeAllowance 0.01 --streamIDs YbtzuXrK0W
```