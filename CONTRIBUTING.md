# Contributing

Thank you for sharing back to the SpeckleGSA project!

These guidelines are simple and should make it easy for you to share your improvements to SpeckleGSA with the Speckle and GSA communities!

All contributions are welcome: bug fixes, documentation, tutorials, new features!

Please set up a pull request to the `dev` branch for all contributions - this will let us discuss and review the changes before incorporating them into the main code base. If this is your first time contributing to SpeckleGSA, please fork the repo and set up a merge request from your repo.

Check out [http://makeapullrequest.com/]() or [https://www.firsttimersonly.com/]() if you have never contributed to an open source project before!

All contributions should be make SpeckleGSA better, so please make sure that your changes do not break SpeckleGSA, or remove existing functionality.

If you would like to make a significant change, please open an issue to discuss it first and be sure to tag @daviddekoning and @nic-burgers-arup.

## Building SpeckleGSA

### Requirements

- Visual Studio 2019
- .NET Framework 4.7.1

### Dev Notes

The SpeckleGSA repo is currently made up of the following projects:
- SpeckleGSA: main project with receiver and sender and GSA class objects
- SpeckleGSAUI: user interface

### Building Process

SpeckleGSA depends on Speckle being installed. Install Speckle using the Speckle installer [here](https://speckle.systems/docs/essentials/start).

- Clone/fork the repo
- Restore all Nuget package missing on the solution
- Set SpeckleGSAUI as start project and rebuild all

### Release process

This process is just to prepare this prerequisite artifact for inclusion in the SpeckleStructuralSuite-installer release process.  When the release process for SpeckleStructuralSuite-installer is invoked, it will include the latest SpeckleGSA artifact resulting from the process below:

- Update versions in AssemblyInfo.cs for both SpeckleGSA and SpeckleGSAUI projects to incremented version
- Merge into master and push - (test if a build is triggered)
- Check that the build artefacts can be downloaded