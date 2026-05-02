<h1 align="center">Welcome to NSYNK Hyperslides Core Framework 👋</h1>
<p>
  <img alt="Version" src="https://img.shields.io/badge/version-0.1.4-blue.svg?cacheSeconds=2592000" />
  <a href="https://github.com/nsynkde/hyperslides-framework/blob/main/Assets/HyperslidesCore/CHANGELOG.md" target="_blank">
    <img alt="Documentation" src="https://img.shields.io/badge/documentation-yes-brightgreen.svg" />
  </a>
</p>

> This is the Hyperslides Unity Core project.

## Author

👤 **NSYNK Gesellschaft für Kunst und Technik mbH**

* Website: https://nsynk.de/
* LinkedIn: [@https:\/\/www.linkedin.com\/company\/nsynk\/](https://linkedin.com/in/https:\/\/www.linkedin.com\/company\/nsynk\/)

## Contents

- [Core Framework]
  * [Installation](#installation)
    + [Versions](#versions)
  * [App life cycle](#app-life-cycle)
    + [Important note](#important-notes)
  * [Important Scripts](#important-scripts)
  * [Bugs and limitations](#bugs-and-limitations)

## Installation
As Unity package via Package Manager with the URL "git@github.com:nsynkde/hyperslides-framework.git?path=/Assets/HyperslidesCore"

### Versions
| Version | Description |
|---|---|
| 0.1.4 | Working framework with user synchronization and first versions of moderator pointers |

## App life cycle
When starting the application on the device, the [XRDataManager](#XRDataManager.cs) sends a webrequest towards the web app server to get the latest JSON data for all presentations.
If this fails, it will load a backup from the [Nakama](#Nakama) Storage API and fallsback to a local JSON file, if this also fails. The [XRUIManager](#XRUIManager.cs) loads the initial UI and waits for updates when joining the match.
When the data has been received, the [XRNetworkmanager](#XRNetworkManager.cs) gets called and initializes the connection to the [Nakama](#Nakama) Server.

### Important notes
The handling of UI visibility, match creation and joining is still work in progress and needs to be updated, when the UI is cleaned up.

## Important Scripts
| Scripts | Description |
|---|---|
| XRDataManager.cs | Handles webrequests to the server and initial load of content as well as backups |
| XRNetworkManager.cs | The NakamaUser, which handles all local and remote player creation as well as updating joining and reconnecting to matches |
| XRSlideManager.cs | Handles the updating, animation and triggering of slide relevant data like animations and visual updates |
| XRAnchorManager.cs | Responsible to update found anchors and fire events to update content anchored to specific worldanchors |
| XRUIManager.cs | Handles the UI updates like switching between client and moderator and other events |
| XRInputManager.cs | Handles input of the user. Will also handle hand tracking in the future, which is currently implemented in the XRPlayer.cs |
| XRSlideElement.cs | This class handles the visbility of its own child contents. It relies on the XRSlideManagers events to update itself |
| XRPlayer.cs | The player class holding all necessary values to sync its position and rotation as well as the pointer visibility as moderator |
| XRNetworkObjects.cs | A collection of simplified classes based on runtime classes to reduce network data load |

## Bugs and limitations
Currently, the visionOS Unity package, which is handling the hand tracking, returns joints being not tracked and therefore a lot of handshape checks fail. This seems to be a limitation on the AVP for now, because this is how Apple returns the state of joints. To handle this issue, we need to copy the package into the root /Packages/ folder of the project and change the VisionOSHandProvider.cs script on line 147 to always return true. This is a temporary solution from Unity devs on forums and will stay until we get an update, that handles this issue
