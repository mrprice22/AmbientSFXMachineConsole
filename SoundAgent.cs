using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;

namespace AmbientAgents
{
    class SoundAgent
    {
        private readonly string _folderPath;
        private List<string> _wavFiles;
        private readonly string _agentName;

        private readonly int _minMinutes;
        private readonly int _maxMinutes;
        private readonly string _mode;

        private int _currentIndex = 0;
        private readonly Random _rand = new Random();

        private int _playlistIndex = 0;

        public SoundAgent(string folderPath)
        {
            _folderPath = folderPath;
            _agentName = Path.GetFileName(folderPath);

            var configPath = Path.Combine(folderPath, "settings.config");
            if (!File.Exists(configPath))
                throw new FileNotFoundException("Missing settings.config");

            var config = File.ReadAllLines(configPath)
                             .Select(line => line.Trim())
                             .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                             .Select(line => line.Split('='))
                             .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim());

            _minMinutes = config.ContainsKey("min_minutes") ? int.Parse(config["min_minutes"]) : 3;
            _maxMinutes = config.ContainsKey("max_minutes") ? int.Parse(config["max_minutes"]) : 6;
            _mode = config.ContainsKey("mode") ? config["mode"].ToLower() : "random";

            _wavFiles = Directory.GetFiles(folderPath, "*.wav")
                      .OrderBy(f => f) // Alphabetical order (good for "01.wav", "02.wav", etc.)
                      .ToList();

            if (_wavFiles.Count == 0)
                throw new InvalidOperationException("No .wav files found in agent folder");

            if (_mode == "shuffle")
                _wavFiles = _wavFiles.OrderBy(x => _rand.Next()).ToList();

            Console.WriteLine($"[AGENT {_agentName}] Initialized with {_wavFiles.Count} .wav files, mode={_mode}, delay={_minMinutes}-{_maxMinutes} min");
        }

        public void RunLoop()
        {
            while (true)
            {
                int waitMinutes = _rand.Next(_minMinutes, _maxMinutes + 1);
                for (int i = waitMinutes; i > 0; i--)
                {
                    Console.WriteLine($"[AGENT {_agentName}] Next sound in {i} min...");
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                }

                var fileToPlay = SelectNextFile();
                PlaySound(fileToPlay);
            }
        }

        private string SelectNextFile()
        {
            if (_wavFiles.Count == 0)
            {
                Console.WriteLine($"[AGENT {_agentName}] No audio files found.");
                return null;
            }

            switch (_mode)
            {
                case "sequential":
                    var seqFile = _wavFiles[_currentIndex];
                    Console.WriteLine($"[AGENT {_agentName}] Sequential index: {_currentIndex}");
                    _currentIndex = (_currentIndex + 1) % _wavFiles.Count;
                    return seqFile;

                case "shuffle":
                    var shuffledFile = _wavFiles[_currentIndex];
                    Console.WriteLine($"[AGENT {_agentName}] Shuffle index: {_currentIndex}");
                    _currentIndex++;
                    if (_currentIndex >= _wavFiles.Count)
                    {
                        _currentIndex = 0;
                        _wavFiles = _wavFiles.OrderBy(x => _rand.Next()).ToList();
                        Console.WriteLine($"[AGENT {_agentName}] Shuffle reshuffled playlist");
                    }
                    return shuffledFile;

                case "random":
                default:
                    var randFile = _wavFiles[_rand.Next(_wavFiles.Count)];
                    Console.WriteLine($"[AGENT {_agentName}] Randomly selected: {Path.GetFileName(randFile)}");
                    return randFile;
            }
        }

        private void PlaySound(string path)
        {
            try
            {
                Console.WriteLine($"[AGENT {_agentName}] Playing: {Path.GetFileName(path)}");
                using (var player = new SoundPlayer(path))
                {
                    player.PlaySync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AGENT {_agentName}] ERROR playing sound: {ex.Message}");
            }
        }
    }
}
