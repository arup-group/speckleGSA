# SpeckleGSA

![specklegsa-logo](https://gitlab.arup.com/tor_digital/SpeckleGSA/raw/3bbd7b153b3754226fcb0277c5b77788903b0be4/readme/ICON.png)

SpeckleGSA is currently under development.

Requires GSA 10 beta.

## Contents

- [Installation](#installation)
- [Usage](#usage)
- [Bugs and feature requests](#bugs-and-feature-requests)
- [Building SpeckleGSA](#building-specklegsa)
- [About Speckle](#about-speckle)

## Installation

SpeckleGSA is not currently packaged for distribution. To build SpeckleGSA yourself, see [Building SpeckleGSA](#building-specklegsa).

## Usage

![specklegsa-demo](https://gitlab.arup.com/tor_digital/SpeckleGSA/raw/3bbd7b153b3754226fcb0277c5b77788903b0be4/readme/demo.gif)

SpeckleGSA implements key components of a Speckle client in it's tab interface:
- Server:
    - Allows users to login to a SpeckleServer
- Stream List:
    - Populates a table with the streams associated with the user's account
- Stream Operation:
    - Perform operations to modify streams
    - **IMPORTANT:** Functionality for most of these are yet to be implemented or are not currently working as intended
- Sender:
    - Sends model to SpeckleServer
    - Allows for sending of either the analysis or design layer of GSA
- Receiver:
    - Receive stream(s) from a SpeckleServer
    - Allows for writing to either the analysis or design layer of GSA
- Settings

## Bugs and Feature Requests

SpeckleGSA is still currently under (rapid) development which can cause many quick changes to occur. If there are any major bugs, please submit a new [issue](https://gitlab.arup.com/tor_digital/SpeckleGSA/issues).

## Building SpeckleGSA

### Requirements

- Visual Studio 2017
- .NET Framework 4.5

### Dev Notes

The SpeckleGSA repo is currently made up of the following projects:
- SpeckleCore: submodule
- SpeckleGSA: main project with receiver and sender
- SpeckleGSACommon: GSA class objects, helper functions, static classes
- SpeckleGSAConverter: converter logic for all GSA (and other) objects
- SpeckleGSAUI: front end of SpeckleGSA

### Building Process

- Clone/fork the repo
- Restore all Nuget package missin on the solution
- If SpeckleCore hasn't been cloned, run `git submodule update --remote`
- Set SpeckleGSAUI as start project and rebuild all

## About Speckle

Speckle reimagines the design process from the Internet up: an open source (MIT) initiative for developing an extensible Design & AEC data communication protocol and platform. Contributions are welcome - we can't build this alone!