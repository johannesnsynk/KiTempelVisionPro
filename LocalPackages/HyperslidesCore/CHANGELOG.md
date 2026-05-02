---
uid: hyperslides-framework-changelog
---
# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.7.21] - 2024-08-07

### Fixed
- Presenter notes panel was calling the wrong method to restart anchor setup

## [0.7.20] - 2024-08-02

### Fixed
- Do not add public sessions, when autojoin is enabled, to prevent autojoining to those

## [0.7.19] - 2024-07-28

### Fixed
- Input Manager now gets selected object correctly depending on the input phase
- Remove maincamera tag from spectator copy

### Removed
- Removed old samples and other unnecessary assets

## [0.7.15] - 2024-07-12

### Fixed
- Camera will update depth and therefore ignore the stored camera values
- Device role will update from notification if not keeping up with nakama user metadata

## [0.7.14] - 2024-07-05

### Changed
- Moderator notes will hide on leaving match
- Role and tracking switch only available on iOS when in match list screen

## [0.7.13] - 2024-07-05

### Changed
- Anchor setup now shows the correct anchors instead of previous once

## [0.7.0 - 0.7.12] - 2024-07-05

### Fixed
- Spectatorview now correctly renders itself plus overlay of main camera, which is being stored on app start in a disabled camera component
- Network synced objects do not create a copy of the class anymore but rather take the same object for all checks
- Slider will not update itself from network when being animated with the animation library
- World anchor prefabs should now hide correctly in relation to role and device being used

### Changed
- Owner of network synced objects is now being reset on server and will update only on interaction
- Input user position is now provided as maincamera position or xr content root on spectator
- Old samples and unnecessary assets have been removed
- Removed settings fields we do not need any more like local ips or nakama users
- Core not checking for disabled slides anymore but rely on the api response to not include those
- New admin role hides moderator panels to be able to edit scenes later

### Added
- Spectator View has been moved from project to internal core
- You can now switch between tracking types and roles on device to test out or present
- Settings do have a trackingtype field now, where you can set the first tracking type that should be used on app start. You can use anchor based tracking, image tracking which will take the first position marker as trackable or you can use free, which will always reset XRContentRoot to your current device position.
- you can now use swipe to control the presentation on touch devices

## [0.6.0] - 2024-06-12

### Fixed
- Network issues are fixed, where wrong order of network calls resulted in jumping position updates
- Fixed too fast skipping in slides with a threaded timer, while its length can be set in the global setting asset
- Fixed rotation of presenter notes to always target the user

### Changed
- Refactored XRSlideManager to unload and load addressables consecutively to avoid memory limit crashes
- This results in dissolve fades not being blocked by mainthread loading assets
- XRSlideElements is adapted to the new XRSlideManager behaviour and will get prepared before loading and unloading assets
- Separated and minified network classes to reduce network message size

### Added
- XRSlider is now available to be used with NetworkSynced components
- Added minor UI improvements and a participant UI triggered by the webapp
- Moderators can now change the username of the device in the matchmaking screen
- Notifications can now be reseived from Nakama server and trigger UI and data updates

## [0.5.2] - 2024-06-05

### Fixed
- Addressables were loaded multipletimes when slides were changing too fast. Now we only are able to have one async method running
- VideoPlayer now checks for m_Clip to be !null
- NetworkSynced has been refactored and is updating now in the correct manner
- Fixed metadata object to fit new json structure now

### Changed
- Synced objects are not checking for owner but for local player interacting with it or not (might introduce ownership later, if we want to restrict to a user)

## [0.4.3] - 2024-06-05

### Added
- People occlusion for iOS

## [0.4.1] - 2024-06-03

### Added
- Addressable load and unload in a editorscene

### Changed
- Update to new version after refactoring spectatormode
- Devices are now connecting directly to the first match it finds if set in settings
- DebugUI has been removed for now

## [0.3.4] - 2024-06-03

### Added
- Spectator mode for standalone builds

### Changed
- OpCodes have been changed due to 0 index limit on nakama server

## [0.3.3] - 2024-06-02

### Fixed
- SlideElement now does not hide active elements with the state if fullyvisible

## [0.3.2] - 2024-05-29

### Added
- Added avatar example model to the player prefab
- Slides can now be prev next changed by arrow keys on editor and standalone builds
- Callback for notification to reload updated data from server

### Changed
- Visibility state is now available on slidemanager and shows/hides only slideelements that need to be in that current dissolve state

### Fixed
- Fixed presentation contents stacking up when changing presentations

## [0.3.1] - 2024-05-28

### Changed
- Metadata is now being send by the user on server connect, join match and if battery difference is > 5%

## [0.3.0] - 2024-05-28

### Added
- Added addressables system to slidemanager and slideelements

### Changed
- SlideElements are now able to handle Assetreferences and load async prefabs based on addressables system
- Added XRSlideDissolver to be able to call slidemeent events inside addressables

## [0.2.3] - 2024-05-27

### Added
- Real device check now checks for foveated rendering, which is not supported aside from real devices

### Changed
- AR Anchoring setup is now more guided and stable through image and world anchors

### Fixed
- Fixed pointer jittering in reversing and combining joint data
- UI should now stick to hands on device and to camera in simulator

## [0.2.2] - 2024-05-24

### Changed
- ModeratorUI is now in the palm of your hand inside a match and in front when setting up

## [0.2.1] - 2024-05-23

### Fixed
- XRSlideElements are now resetting correctly on leaving match and rejoining
- Rotation of all players is now correctly calculated to account the XRRootContent offset set by anchors

## [0.2.0] - 2024-05-22

### Added
- A new abstract class called XRSlideDissolveComponent is now available to hook into the closest XRSlideElement and react to its dissolve and slidechange values
- You now can use the ReadOnly Attribute to show public or serialized fields without being able to change them in the inspector

### Changed
- Refactored the XRSlideManager to completely ignore any VisibilityStates
- All XRSlideElements now handle their own state and react to the different dissolve variations on their own

## [0.1.6] - 2024-05-22

### Changed
- Use IP address instead of System.deviceUniqueIdentifier for Nakama authentication

## [0.1.5] - 2024-05-22

### Added
- A new setting scriptableobject is now inside the package. You can duplicate it for your project and change IDs, URLs and other values

### Changed
- Players are now updated by a fixed tick rate

### Fixed
- The rotation is now split between player root and head transform for better rotation handling (there seems to be an offset in the rotation still for the head)
- The pointers are not updated correctly on all players and are delivered through the new transform array

## [0.1.4] - 2024-05-18

### Changed
- Hand mesh is now deactivated and only pointer and impact marker are triggered

### Fixed
- Calculation of positions is now correct to markers and other sessions across devices
- Fixed blurry app icon
- Anchors are now correctly set, when recognized again and can be reset with markers
- Fixed a coding bug, where the components were destroyed, but not the gameobjects, leading to mass creation of empty gameobjects

## [0.1.3] - 2024-05-18

### Fixed
- Material missing on prefabs is replaced by package materials

## [0.1.2] - 2024-05-17

### Added
- Handtracking has now been added
- Pointers are now synchronized over network and can be used by moderators

### Changed
- Samples are replaced or removed and the scene holds a prefab, you can reuse, so you do not accidentally forget prefabs or whatever. Just create an empty scene, throw the prefab XRCoreFrameworkPrefab in and you are good to go for the scene setup

### Fixed
- The package com.unity.xr.visionos has a bug, that prevents joints from being suggested, for now we force this with a custom copy of the package under /Packages

## [0.1.1] - 2024-05-15

### Fixed
- Minor Bugfixes

## [0.1.0] - 2024-05-14

### Added
- New output values for XRSlideElement script

### Changed
- Changed naming from XRSlideRuntime to XRSlideElement and removed prefab.
- ATTENTION, this might break your current XRSlideRuntime script and you have to readd it and the values

## [0.0.4] - 2024-05-14

### Added
- Reference image from visionos samples to avoid missing references when not installed

### Changed
- Updates package json to avoid dependency overhead with default unity projects

## [0.0.3] - 2024-05-13

### Added
- Changelog document and new structure of the core unity project

### Changed
- Project structure to be used as a framework in other unity projects
- Bundle identifier is now pointing to NSYNK

### Deprecated

### Removed

### Fixed

### Security