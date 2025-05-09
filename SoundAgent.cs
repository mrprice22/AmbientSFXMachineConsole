﻿using System;
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

        // Balance related
        private int _balanceMin = 50;
        private int _balanceMax = 50;
        private int _balanceInvertChance = 0;

        // Turbo mode related
        private int _turboChance = 0;
        private int _turboMinFires = 2;
        private int _turboMaxFires = 5;
        private bool _inTurboMode = false;
        private int _remainingTurboPlays = 0;
        private int _cooldownAfterTurbo = 0; // seconds

        private int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

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

            // Balance settings
            if (config.ContainsKey("balance_min")) _balanceMin = Clamp(int.Parse(config["balance_min"]), 0, 100);
            if (config.ContainsKey("balance_max")) _balanceMax = Clamp(int.Parse(config["balance_max"]), 0, 100);
            if (_balanceMin > _balanceMax) Swap(ref _balanceMin, ref _balanceMax);

            if (config.ContainsKey("balance_invert_chance"))
                _balanceInvertChance = Clamp(int.Parse(config["balance_invert_chance"]), 0, 100);

            // Turbo settings
            if (config.ContainsKey("turbo_chance"))
                _turboChance = Clamp(int.Parse(config["turbo_chance"]), 0, 100);

            if (config.ContainsKey("turbo_min_fires"))
                _turboMinFires = Math.Max(1, int.Parse(config["turbo_min_fires"]));

            if (config.ContainsKey("turbo_max_fires"))
                _turboMaxFires = Math.Max(1, int.Parse(config["turbo_max_fires"]));

            if (_turboMinFires > _turboMaxFires) Swap(ref _turboMinFires, ref _turboMaxFires);

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

            Console.WriteLine($"[AGENT {_agentName}] Initialized with {_audioFiles.Count} audio files, mode={_mode}, volume={_volume}%, balance={_balanceMin}-{_balanceMax}, turbo_chance={_turboChance}%");
        }

        public void RunLoop()
        {
            if (!_enabled) return;

            while (true)
            {
                int totalMilliseconds;

                if (_cooldownAfterTurbo > 0)
                {
                    totalMilliseconds = _cooldownAfterTurbo * 1000; // Convert cooldown time to milliseconds
                    _cooldownAfterTurbo = 0;
                    Console.WriteLine($"[AGENT {_agentName}] Cooling down after turbo: waiting {TimeSpan.FromMilliseconds(totalMilliseconds):mm\\:ss}...");
                }
                else if (_playCounter == 0 && _overrideStartupSeconds > 0)
                {
                    totalMilliseconds = _rand.Next(0, _overrideStartupSeconds + 1) * 1000; // Convert to milliseconds
                }
                else if (_inTurboMode)
                {
                    // In turbo mode, treat min_seconds and max_seconds as milliseconds
                    totalMilliseconds = _rand.Next(_minSeconds * 1000, _maxSeconds * 1000 + 1); // Convert to milliseconds
                    Console.WriteLine($"[AGENT {_agentName}] Next sound in {TimeSpan.FromMilliseconds(totalMilliseconds):mm\\:ss}... (Turbo Mode)");
                }
                else
                {
                    totalMilliseconds = (_rand.Next(_minMinutes * 60, (_maxMinutes * 60) + 1) + _rand.Next(_minSeconds, _maxSeconds + 1)) * 1000; // Convert total time to milliseconds
                    Console.WriteLine($"[AGENT {_agentName}] Next sound in {TimeSpan.FromSeconds(totalMilliseconds / 1000):mm\\:ss}...");
                }

                Thread.Sleep(totalMilliseconds); // Sleep for the correct amount of time in milliseconds


                if (!_inTurboMode && _rand.Next(100) < _turboChance)
                {
                    _inTurboMode = true;
                    _remainingTurboPlays = _rand.Next(_turboMinFires, _turboMaxFires + 1);
                    Console.WriteLine($"[AGENT {_agentName}] TURBO MODE activated for {_remainingTurboPlays} plays!");
                }

                var fileToPlay = SelectNextFile();
                if (!string.IsNullOrEmpty(fileToPlay))
                {
                    _playCounter++;
                    PlaySound(fileToPlay);

                    if (_inTurboMode)
                    {
                        _remainingTurboPlays--;
                        if (_remainingTurboPlays <= 0)
                        {
                            _inTurboMode = false;
                            _cooldownAfterTurbo = _rand.Next(_minMinutes * 60, (_maxMinutes * 60) + 1) + _rand.Next(_minSeconds, _maxSeconds + 1);
                            Console.WriteLine($"[AGENT {_agentName}] TURBO MODE ended. Cooldown for {_cooldownAfterTurbo} seconds.");
                        }
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

                // Load the audio file
                using var audioFile = new AudioFileReader(path) { Volume = _volume / 100f };
                using var outputDevice = new WaveOutEvent();

                // Convert stereo to mono if needed
                ISampleProvider sampleProvider;
                if (audioFile.WaveFormat.Channels == 2)
                {
                    sampleProvider = new StereoToMonoSampleProvider(audioFile);
                }
                else
                {
                    sampleProvider = audioFile; // Keep stereo if it's already mono
                }

                // Default balance handling (50 means no pan)
                float balance = (_rand.Next(_balanceMin, _balanceMax + 1) - 50) / 50f;

                // Invert balance with a chance if required
                if (_rand.Next(100) < _balanceInvertChance)
                    balance = -balance;

                // If the balance is exactly 0.5 (center), just play stereo without pan
                if (_balanceMin == 50 && _balanceMax==50)
                {
                    outputDevice.Init(audioFile); // Stereo (no panning)
                }
                else
                {
                    // Apply panning if balance is not neutral
                    var panProvider = new PanningSampleProvider(sampleProvider) { Pan = ClampFloat(balance, -1f, 1f) };
                    outputDevice.Init(panProvider); // Apply panned audio
                }

                // Play the sound
                outputDevice.Play();

                // Wait for playback to finish
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



        private static void Swap(ref int a, ref int b)
        {
            int temp = a;
            a = b;
            b = temp;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }
}
