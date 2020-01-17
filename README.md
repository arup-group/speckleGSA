# SpeckleGSA
![specklegsa-demo](https://gitlab.arup.com/speckle/SpeckleGSA/raw/b6be19d9e463f0aac00d4688e39084368ef8ecda/readme/demo.gif)

Requires GSA 10.

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

- Visual Studio 2019
- .NET Framework 4.7.1

### Dev Notes

The SpeckleGSA repo is currently made up of the following projects:
- SpeckleGSA: main project with receiver and sender and GSA class objects
- SpeckleGSAUI: user interface

### Building Process

SpeckleGSA depends on Speckle being installed. Install Speckle using the Speckle installer [here](https://speckle.works/builds/).

- Clone/fork the repo
- Restore all Nuget package missing on the solution
- Set SpeckleGSAUI as start project and rebuild all

### Release process

This process is just to prepare this prerequisite artifact for inclusion in the SpeckleStructuralSuite-installer release process.  When the release process for SpeckleStructuralSuite-installer is invoked, it will include the latest SpeckleGSA artifact resulting from the process below:

- Update versions in AssemblyInfo.cs for both SpeckleGSA and SpeckleGSAUI projects to incremented version
- Merge into master and push - (test if a build is triggered)
- Trigger a build at https://gitlab.arup.com/speckle/SpeckleGSA/pipelines by clicking "Run Pipeline"
- Check that the build artefacts can be downloaded

## About Speckle

Speckle reimagines the design process from the Internet up: an open source (MIT) initiative for developing an extensible Design & AEC data communication protocol and platform. Contributions are welcome - we can't build this alone!

## Notes

SpeckleGSA is maintained by [Nic Burgers](https://gitlab.arup.com/Nic.Burgers).
