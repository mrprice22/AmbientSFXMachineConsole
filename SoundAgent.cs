using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Codecs;
using NAudio.Wave;

namespace AmbientAgents
{
    class SoundAgent
    {
        private readonly string _folderPath;
        private List<string> _audioFiles;
        private readonly string _agentName;
        private readonly Random _rand;
        private int _minMinutes;
        private int _maxMinutes;
        private int _minSeconds;
        private int _maxSeconds;
        private int _overrideStartupSeconds; //optional, if specified will override the delay for the first sound by a random ammount of seconds (up to this value)
        private string _mode;
        private int _volume; // 0–100
        private bool _enabled;
        private int _currentIndex = 0;
        private int _playCounter = 0;
        private int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
        public SoundAgent(string folderPath)
        {
            _folderPath = folderPath;
            _agentName = Path.GetFileName(folderPath);
            _rand = new Random();

            var configPath = Path.Combine(folderPath, "settings.config");
            if (!File.Exists(configPath))
                throw new FileNotFoundException("Missing settings.config");

            var config = File.ReadAllLines(configPath)
                             .Select(line => line.Trim())
                             .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                             .Select(line => line.Split('='))
                             .ToDictionary(kv => kv[0].Trim().ToLower(), kv => kv[1].Trim());

            _minMinutes = config.ContainsKey("min_minutes") ? int.Parse(config["min_minutes"]) : 3;
            _maxMinutes = config.ContainsKey("max_minutes") ? int.Parse(config["max_minutes"]) : 6;
            _minSeconds = config.ContainsKey("min_seconds") ? int.Parse(config["min_seconds"]) : 0;
            _maxSeconds = config.ContainsKey("max_seconds") ? int.Parse(config["max_seconds"]) : 0;
            _overrideStartupSeconds = config.ContainsKey("override_startup_seconds") ? int.Parse(config["override_startup_seconds"]) : 0;
            _mode = config.ContainsKey("mode") ? config["mode"].ToLower() : "random";
            _volume = config.ContainsKey("volume") ? Clamp(int.Parse(config["volume"]), 0, 100) : 100;
            _enabled = config.ContainsKey("enabled") ? bool.Parse(config["enabled"]) : true;

            if (!_enabled)
            {
                Console.WriteLine($"[AGENT {_agentName}] Disabled in settings.");
                return;
            }

            _audioFiles = Directory.GetFiles(folderPath)
                .Where(f => f.EndsWith(".wav") || f.EndsWith(".mp3") || f.EndsWith(".ogg"))
                .OrderBy(f => f)
                .ToList();

            if (_audioFiles.Count == 0)
                throw new InvalidOperationException("No supported audio files found in agent folder");

            if (_mode == "shuffle")
                _audioFiles = _audioFiles.OrderBy(x => _rand.Next()).ToList();

            Console.WriteLine($"[AGENT {_agentName}] Initialized with {_audioFiles.Count} audio files, mode={_mode}, volume={_volume}%, delay={_minMinutes}-{_maxMinutes} min + {_minSeconds}-{_maxSeconds} sec");
        }

        public void RunLoop()
        {
            if (!_enabled) return;

            while (true)
            {
                int totalSeconds;
                if (_playCounter == 0 && _overrideStartupSeconds > 0 ){
                    totalSeconds = _rand.Next(0, _overrideStartupSeconds + 1);
                } else {
                    totalSeconds = _rand.Next(_minMinutes * 60, (_maxMinutes * 60) + 1) + _rand.Next(_minSeconds, _maxSeconds + 1);        
                }
                Console.WriteLine($"[AGENT {_agentName}] Next sound in {TimeSpan.FromSeconds(totalSeconds):mm\\:ss}...");
                Thread.Sleep(TimeSpan.FromSeconds(totalSeconds));

                var fileToPlay = SelectNextFile();
                if (!string.IsNullOrEmpty(fileToPlay)) {
                    _playCounter++;
                    PlaySound(fileToPlay);
                }
            }
        }

        private string SelectNextFile()
        {
            if (_audioFiles.Count == 0) return null;

            switch (_mode)
            {
                case "sequential":
                    var seqFile = _audioFiles[_currentIndex];
                    _currentIndex = (_currentIndex + 1) % _audioFiles.Count;
                    return seqFile;

                case "shuffle":
                    var shuffleFile = _audioFiles[_currentIndex];
                    _currentIndex++;
                    if (_currentIndex >= _audioFiles.Count)
                    {
                        _currentIndex = 0;
                        _audioFiles = _audioFiles.OrderBy(x => _rand.Next()).ToList();
                    }
                    return shuffleFile;

                case "random":
                default:
                    return _audioFiles[_rand.Next(_audioFiles.Count)];
            }
        }

        private void PlaySound(string path)
        {
            try
            {
                Console.WriteLine($"[AGENT {_agentName}] Playing: {Path.GetFileName(path)}");

                using var audioFile = new AudioFileReader(path) { Volume = _volume / 100f };
                using var outputDevice = new WaveOutEvent();
                outputDevice.Init(audioFile);
                outputDevice.Play();

                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AGENT {_agentName}] ERROR playing sound: {ex.Message}");
            }
        }
    }
}
