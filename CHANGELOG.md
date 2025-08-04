## [1.3.3] - 2025-06-12

### Added

- FMOD 2.03 Support

### Changed

- Removed automated tests

### Fixed

- Notification script failing to load in Unity 2021 and below

## [1.3.2] - 2025-06-05

### Added

- Ability to capture photos
- Camera stabilization space can now be defined, allowing for player snap-turning

### Changed

- Improved support for Meta Hands interaction

### Fixed

- Optimised system behaviour to reduce performance impact when LCK is not in use
- Disabled AudioListener components are now ignored
- Camera FOV resetting when using Meta Interaction

## [1.3.1] - 2025-04-25

### Fixed

- New quality option button missing collider on the Direct Tablet variant

## [1.3.0] - 2025-04-23

### Added

- A save screen now pops up on the tablet notifying about the video being done saving
- All tablet buttons now use the discreet audio API to not be included in recordings
- Added video quality options button to tablet, with SD/HD buttons on Standalone, and additional 2K and 4K option on Windows
- Exposed GetActiveCamera method and ActiveCameraSet callback in the LCKService API

### Changed

- Default android audio bitrate set to 192kb
- Recordings are now saved to Movies Folder

### Fixed

- Video and audio artifacts at the beginning of recordings

## [1.2.4] - 2025-04-03

### Added

- Soft limiting function to mixed audio, reducing artifacts with loud audio. This can be disabled in LckSettings

### Fixed

- Crash when LckSettings asset could not be loaded
- Record button state incorrect when disabling tablet immediately after stopping recording

## [1.2.3] - 2025-03-12

### Fixed

- An EGL context creation issue which resulted in recordings failing with ERROR when using OpenGL

## [1.2.2] - 2025-03-03

### Fixed

- Memory leak that resulted in some memory not being freed after a recording on Android and Dx11 on Windows

## [1.2.1] - 2025-02-27

### Changed

- Video samplerate now follows the output samplerate of Unity or the active third party audio engine, or a fallback set in LCK Settings

### Fixed

- WWise audio capture issues

## [1.2.0] - 2025-02-12

### Added

- Pipeline now allows devs to implement ILckAudioSource to supply custom game audio to the capture
- Wwise audio engine support
- GL logging control in LCK settings
- SetPreviewEnabled to LckService for enabling and disabling capture while not recording

### Changed

- Audio handling is completely revamped
- LCK Tablet prefab has been restructured to be input system agnostic, and LCK by default comes with variants for Meta XR and XRITK support
- XRTIK is no longer a dependency

### Fixed

- Various audio delay issues
- Microphone capture issues

## [1.1.6] - 2025-01-10

### Added

- OpenGL support
- SetTrackDescriptor, SetTrackBitrate, SetTrackAudioBitrate, SetMicrophoneGain and SetGameAudioGain to LckService
- Explicit game name field in LCK settings, separate from Unity Project name

### Changed

- Moved sync point between the game and the LCK encoder to earlier in the frame, increasing performance on android
- LckServiceHelper now asks for microphone permissions on awake, on android. This can be disabled on the service helper.

### Fixed

- Issues with microphone toggle not behaving intuitively
- Issues where the LCK Tablet preview would incorrectly interpret data in the alpha channel of the capture as transparency
- Crash caused by spamming certain actions relating to microphone and starting/stopping recording
- The editor hanging when using an input device that does not provide samples
- Shader issues on the tablet in Unity 2021
- Slow encoding when using Vulkan on desktop, causing videos to take a long time to save
- Issues building when using minified builds

## [1.1.5] - 2024-11-18

### Fixed

- Crash when muting/unmuting rapidly
- LCK Tablet not properly hiding in selfie mode
- Recording telemetry being serialized wrong
- LCK Tablet orientation button misbehaving
- Unsupported platforms not being excluded from NativeGallery and NativeAudio assembly defs

## [1.1.4] - 2024-11-12

### Added

- Added Unity 2020 support

### Changed

- LCK Tablet hierarchy and transform cleanup
- Improved error handling and telemetry in the LckMixer

### Fixed

- Error spam related to RecordingTime in case the service is unavailable

## [1.1.3] - 2024-11-08

### Fixed

- Issue with SetTrackFramerate
- Potential audio related crash on Windows

## [1.1.2] - 2024-11-05

### Added

- Project settings for disabling location telemetry and device telemetry

### Fixed

- NativeAudio error when playing in editor with Android build target
- Improved error reporting and error handling in LckServiceHelper and LckService
- Discreet audio erroring if preloading the same clip multiple times
- Error spam from LCK Tablet when service is not available

## [1.1.1] - 2024-11-01

### Fixed

- Conflict with UnityNativeGallery

## [1.1.0] - 2024-10-31

### Added

- Discreet audio playback API to LckService, allowing playing AudioClips that is not picked up in recordings
- Discreet audio cues for recording started / stopped to LCK Tablet
- OnRenderTextureSet event on LckMonitor, allowing easier output monitoring from custom scripts
- Stencil support
- Log filtering options to Project Settings
- SetTrackFramerate API to LckService
- Error reporting in the case of unsupported graphics API or platform

### Changed

- LCK Tablet shaders to support built-in renderer
- LCK Tablet design
- LCK Tablet to manage render layers and culling masks automatically

### Fixed

- Number of LCK Tablet Using Direct issues
- Crashes related to clashes with Unity render pass timing
- Number of smaller general UI issues
- Issues in editor relating to DontDestroyOnLoad
- Off-main-thread unity call errors
- Error reporting and handling in various LckService execution paths

## [1.0.0] - 2024-10-15
- Initial launch
