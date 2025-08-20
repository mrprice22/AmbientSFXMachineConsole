using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AmbientAgents
{
    class SoundAgent
    {
        private readonly string _folderPath;
        private List<string> _audioFiles;
        private readonly string _agentName;
        private readonly Random _rand;
        private int _minMinutes, _maxMinutes;
        private int _minSeconds, _maxSeconds;
        private int _overrideStartupSeconds;
        private string _mode;
        private int _volume;
        private bool _enabled;
        private int _currentIndex = 0;
        private int _playCounter = 0;
        private bool startupDelayUsed = false;


        private readonly List<WaveOutEvent> _activeOutputs = new List<WaveOutEvent>();
        private readonly object _lock = new object();

        // Synchronization handle to block until sound(s) are finished
        private ManualResetEventSlim _playbackDone = new ManualResetEventSlim(true);

        // Balance
        private int _balanceMin = 50;
        private int _balanceMax = 50;
        private int _balanceInvertChance = 0;

        // Turbo
        private int _turboChance = 0;
        private int _turboMinFires = 2;
        private int _turboMaxFires = 5;
        private bool _inTurboMode = false;
        private int _remainingTurboPlays = 0;
        private int _cooldownAfterTurbo = 0;

        // Followup
        private List<string> _followupFiles = new List<string>();
        private string _followupFolder = null;
        private string _followupMode = "random";
        private int _followupVolume = 100;
        private int _followupBalanceMin = 50;
        private int _followupBalanceMax = 50;
        private int _followupBalanceInvertChance = 0;
        private bool _followupEnabledForNormalFiles = false;

        private int Clamp(int value, int min, int max) =>
            (value < min) ? min : (value > max) ? max : value;

        public SoundAgent(string folderPath)
        {
            _folderPath = folderPath;
            _agentName = Path.GetFileName(folderPath);
            _rand = new Random();

            var configPath = Directory.GetFiles(folderPath, "*.config").FirstOrDefault();
            if (configPath == null)
                throw new FileNotFoundException("No .config file found in the folder.");

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

            // Followup setup
            _followupFolder = Path.Combine(_folderPath, "followup");
            if (Directory.Exists(_followupFolder))
            {
                var followupConfigPath = Directory.GetFiles(_followupFolder, "*.config").FirstOrDefault();
                if (followupConfigPath != null)
                {
                    var followupConfig = File.ReadAllLines(followupConfigPath)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                        .Select(line => line.Split('='))
                        .ToDictionary(kv => kv[0].Trim().ToLower(), kv => kv[1].Trim());

                    _followupMode = followupConfig.ContainsKey("mode") ? followupConfig["mode"].ToLower() : _followupMode;
                    _followupVolume = followupConfig.ContainsKey("volume") ? Clamp(int.Parse(followupConfig["volume"]), 0, 100) : _followupVolume;
                    _followupBalanceMin = followupConfig.ContainsKey("balance_min") ? int.Parse(followupConfig["balance_min"]) : _followupBalanceMin;
                    _followupBalanceMax = followupConfig.ContainsKey("balance_max") ? int.Parse(followupConfig["balance_max"]) : _followupBalanceMax;
                    _followupBalanceInvertChance = followupConfig.ContainsKey("balance_invert_chance") ? int.Parse(followupConfig["balance_invert_chance"]) : _followupBalanceInvertChance;
                    _followupEnabledForNormalFiles = followupConfig.ContainsKey("play_normal_files") ? bool.Parse(followupConfig["play_normal_files"]) : _followupEnabledForNormalFiles;
                }

                _followupFiles = Directory.GetFiles(_followupFolder)
                    .Where(f => f.EndsWith(".wav") || f.EndsWith(".mp3") || f.EndsWith(".ogg"))
                    .OrderBy(f => f)
                    .ToList();
            }

            Console.WriteLine($"[AGENT {_agentName}] Initialized with {_audioFiles.Count} audio files, mode={_mode}, volume={_volume}%");
        }

        public void RunLoop()
        {
            if (!_enabled) return;

            while (true)
            {
                // Pick and play sound
                var fileToPlay = SelectNextFile();
                if (string.IsNullOrEmpty(fileToPlay)) continue;

            
                _playbackDone.Reset();
                PlaySound(fileToPlay);

                // ✅ Wait until sound and followup finish
                _playbackDone.Wait();

                // Now calculate and wait the next delay
                int totalMilliseconds;
                if (_cooldownAfterTurbo > 0)
                {
                    totalMilliseconds = _cooldownAfterTurbo * 1000;
                    _cooldownAfterTurbo = 0;
                }
                else if (_playCounter == 1 && _overrideStartupSeconds > 0)
                {
                    totalMilliseconds = _rand.Next(0, _overrideStartupSeconds + 1) * 1000;
                }
                else if (_inTurboMode)
                {
                    totalMilliseconds = _rand.Next(_minSeconds * 1000, _maxSeconds * 1000 + 1);
                }
                else
                {
                    totalMilliseconds =
                        (_rand.Next(_minMinutes * 60, (_maxMinutes * 60) + 1) +
                         _rand.Next(_minSeconds, _maxSeconds + 1)) * 1000;
                }

                _playCounter++;
                Console.WriteLine($"[AGENT {_agentName}] Waiting {TimeSpan.FromMilliseconds(totalMilliseconds):mm\\:ss} until next play...");
                Thread.Sleep(totalMilliseconds);

                // Turbo check
                if (!_inTurboMode && _rand.Next(100) < _turboChance)
                {
                    _inTurboMode = true;
                    _remainingTurboPlays = _rand.Next(_turboMinFires, _turboMaxFires + 1);
                }
                else if (_inTurboMode)
                {
                    _remainingTurboPlays--;
                    if (_remainingTurboPlays <= 0)
                    {
                        _inTurboMode = false;
                        _cooldownAfterTurbo =
                            _rand.Next(_minMinutes * 60, (_maxMinutes * 60) + 1) +
                            _rand.Next(_minSeconds, _maxSeconds + 1);
                    }
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

                var audioFile = new AudioFileReader(path) { Volume = _volume / 100f };
                ISampleProvider sampleProvider = audioFile.WaveFormat.Channels == 2
                    ? (ISampleProvider) new StereoToMonoSampleProvider(audioFile)
                    : audioFile;

                float balance = (_rand.Next(_balanceMin, _balanceMax + 1) - 50) / 50f;
                if (_rand.Next(100) < _balanceInvertChance) balance = -balance;

                var outputDevice = new WaveOutEvent();
                outputDevice.Init(_balanceMin == 50 && _balanceMax == 50
                    ? audioFile
                    : (ISampleProvider) new PanningSampleProvider(sampleProvider) { Pan = ClampFloat(balance, -1f, 1f) });

                lock (_lock) _activeOutputs.Add(outputDevice);

                outputDevice.Play();
                outputDevice.PlaybackStopped += (s, e) =>
                {
                    lock (_lock)
                    {
                        outputDevice.Dispose();
                        _activeOutputs.Remove(outputDevice);
                    }
                    audioFile.Dispose();

                    // Handle followup
                    bool shouldTriggerFollowup =
                        Path.GetFileNameWithoutExtension(path).EndsWith("_followup") || _followupEnabledForNormalFiles;

                    if (shouldTriggerFollowup && _followupFiles.Count > 0)
                    {
                        string followupFile = SelectFollowupFile();
                        if (!string.IsNullOrEmpty(followupFile))
                        {
                            PlayFollowup(followupFile);
                            return; // don’t set _playbackDone yet
                        }
                    }

                    // No followup → mark as done
                    _playbackDone.Set();
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AGENT {_agentName}] ERROR playing sound: {ex.Message}");
                _playbackDone.Set(); // prevent deadlock
            }
        }

        private string SelectFollowupFile()
        {
            if (_followupFiles.Count == 0) return null;
            return _followupMode switch
            {
                "sequential" => _followupFiles[_currentIndex++ % _followupFiles.Count],
                "shuffle" => _followupFiles[_rand.Next(_followupFiles.Count)],
                _ => _followupFiles[_rand.Next(_followupFiles.Count)],
            };
        }

        private void PlayFollowup(string path)
        {
            try
            {
                Console.WriteLine($"[AGENT {_agentName}] Playing FOLLOWUP: {Path.GetFileName(path)}");

                var audioFile = new AudioFileReader(path) { Volume = _followupVolume / 100f };
                ISampleProvider sampleProvider = audioFile.WaveFormat.Channels == 2
                    ? (ISampleProvider) new StereoToMonoSampleProvider(audioFile)
                    : audioFile;

                float balance = (_rand.Next(_followupBalanceMin, _followupBalanceMax + 1) - 50) / 50f;
                if (_rand.Next(100) < _followupBalanceInvertChance) balance = -balance;

                var outputDevice = new WaveOutEvent();
                outputDevice.Init(_followupBalanceMin == 50 && _followupBalanceMax == 50
                    ? audioFile
                    : (ISampleProvider) new PanningSampleProvider(sampleProvider) { Pan = ClampFloat(balance, -1f, 1f) });

                lock (_lock) _activeOutputs.Add(outputDevice);

                outputDevice.Play();
                outputDevice.PlaybackStopped += (s, e) =>
                {
                    lock (_lock)
                    {
                        outputDevice.Dispose();
                        _activeOutputs.Remove(outputDevice);
                    }
                    audioFile.Dispose();

                    // ✅ Only now do we release RunLoop to continue
                    _playbackDone.Set();
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AGENT {_agentName}] ERROR playing followup: {ex.Message}");
                _playbackDone.Set();
            }
        }

        private static float ClampFloat(float value, float min, float max) =>
            (value < min) ? min : (value > max) ? max : value;
    }
}