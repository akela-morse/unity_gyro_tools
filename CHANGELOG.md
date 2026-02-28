# Changelog

Most recent updates will appear first. This is a summary of all pertinent changes to the package.

## [0.1.5] - 2026-02-28

### Added

- New Input control (IMUControl) that packs both accelerometer and gyroscope values into one control

### Fixed

- Code stripping options disabling IMU's during builds
- Conflicts between regular inputs and IMU inputs

### Changed

- Made Dualshock 4 IMU's in windows be accessed through HID to circumvent Unity bug

### Removed

- Delegates subscribed to Input system updates (Due to bugs and pending redesign of IMU system)

## [0.1.0] - 2026-01-06

### Added

- Integrated gyroscope and accelerometer inputs from SDL3 compatble devices into the Unity Input System.
- Added Editor script to automatically add dependencies
- Basic sample to showcase gyroscope functionality

### Fixed

- Incorrect gyroscope time scaling in Object Rotation Sample
- Unity package dependency versions

### Changed

- Made SDL3 polling methods optional

### Removed

- HID overrides for IMU inputs due to lack of cross platform compatibility
