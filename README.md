# AgentSoundPlayer

A lightweight, configurable sound agent that plays audio files with random timing, bursts, and dynamic stereo effects.

## Features
- **Randomized Sound Playback**: Agents pick random audio files to play at intervals you control.
- **Turbo Bursts**: Optional rapid-fire sound bursts with a built-in cool-off period.
- **Stereo Panning**: Dynamic left/right audio balance on each sound.
- **Flexible Config Files**: You can name config files anything you want.
- **Supports MP3 and OGG**: Play `.wav`, `.mp3`, and `.ogg` audio files.
- **Robust Error Handling**: Safer min/max input handling and improved stability.
- **Simple Console Logs**: Easy to see which agents are playing what sounds.

## Usage
1. Prepare a folder with your audio files.
2. Create a JSON config file with your agent settings (timing, volume, balance, etc.).
3. Run the program — agents will automatically load and start playing sounds.


## Requirements
- .NET 6.0 or newer
- [NAudio](https://github.com/naudio/NAudio) library for audio playback

## Setup
- Clone the repo
- Restore NuGet packages
- Build and run!

## License
MIT License.  
Feel free to use, modify, and distribute.

---

*Built with ❤️ for fun, creativity, and a little chaos.*
