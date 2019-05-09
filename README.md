# SpeckleGSA
![specklegsa-demo](https://gitlab.arup.com/speckle/SpeckleGSA/raw/b6be19d9e463f0aac00d4688e39084368ef8ecda/readme/demo.gif)

SpeckleGSA is currently under development.

Requires GSA 10 beta build 30.

## Contents

- [Installation](#installation)
- [Usage](#usage)
- [Bugs and feature requests](#bugs-and-feature-requests)
- [Building SpeckleGSA](#building-specklegsa)
- [About Speckle](#about-speckle)
- [Notes](#notes)

## Installation

SpeckleGSA is bundled as part of the Speckle Structural Suite. Download the latest version [here](https://gitlab.arup.com/speckle/specklestructuralsuite-installer/releases).

## Usage
SpeckleGSA implements key components of a Speckle client in it's tab interface:
- Server:
    - Allows users to login to a SpeckleServer
- GSA:
    - Create or open a GSA file
- Sender:
    - Sends model to a SpeckleServer
- Receiver:
    - Receive stream(s) from a SpeckleServer
- Settings

## Bugs and Feature Requests

SpeckleGSA is still currently under development which can cause many quick changes to occur. If there are any major bugs, please submit a new [issue](https://gitlab.arup.com/speckle/SpeckleGSA/issues).

## Building SpeckleGSA

### Requirements

- Visual Studio 2017
- .NET Framework 4.6.1

### Dev Notes

The SpeckleGSA repo is currently made up of the following projects:
- SpeckleGSA: main project with receiver and sender and GSA class objects
- SpeckleGSAUI: user interface
- SpeckleStructures: submodule
- SpeckleCoreGeometry: submodule
- SpeckleCore: submodule

### Building Process

SpeckleGSA depends on SpeckleStructures being installed. Install SpeckleStructures using the Speckle Structural Suite installer [here](https://gitlab.arup.com/speckle/specklestructuralsuite-installer/-/jobs/artifacts/master/raw/SpeckleStructuralSuite.exe?job=build).

- Clone/fork the repo
- Restore all Nuget package missing on the solution
- If submodules haven't been cloned, run `git submodule update --remote`
- Set SpeckleGSAUI as start project and rebuild all

## About Speckle

Speckle reimagines the design process from the Internet up: an open source (MIT) initiative for developing an extensible Design & AEC data communication protocol and platform. Contributions are welcome - we can't build this alone!

## Notes

SpeckleGSA is written and maintained by [Mishael Nuh](https://gitlab.arup.com/Mishael.Nuh).