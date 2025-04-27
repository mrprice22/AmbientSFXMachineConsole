# Changelog

## [v1.0.0] - 2025-04-26

### Added
- **Turbo Burst Cool-Off**: After a burst of rapid sounds, agents now have a cool-down period to prevent immediate re-triggering.
- **Flexible Config Names**: Config files can now have any filename, not just hardcoded names.
- **MP3 and OGG Support**: Agents can now play `.mp3` and `.ogg` audio files, expanding file format compatibility.
- **Stereo Balance (Panning)**: Agents apply a randomized stereo balance (left/right panning) for more dynamic soundscapes.

### Fixed
- **Min/Max Validation**: All min/max values (volume, balance, etc.) now auto-correct if the user provides them out of order.
- **Audio Playback Stability**: Initialization and error handling improved for more reliable sound playback.

### Improved
- Cleaner console logging to help with tracking active agents and sounds.
- More efficient resource handling during playback.
