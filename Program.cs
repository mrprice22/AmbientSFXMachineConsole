using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms; // Hidden window for app name in mixer/Alt+Tab
using System.Drawing;       // Icon for window

namespace AmbientAgents
{
    class Program
    {
        static readonly string BaseSoundFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snd");

        [STAThread]
        static void Main(string[] args)
        {
            // --- Load Config ---
            string appName = "Default Sound Agent";
            string iconPath = Path.Combine(AppContext.BaseDirectory, "agent.ico");
            string configPath = Path.Combine(AppContext.BaseDirectory, "appSettings.config");

            if (File.Exists(configPath))
            {
                foreach (var rawLine in File.ReadAllLines(configPath))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("appName=", StringComparison.OrdinalIgnoreCase))
                    {
                        appName = line.Substring("appName=".Length).Trim();
                    }
                    else if (line.StartsWith("icon=", StringComparison.OrdinalIgnoreCase))
                    {
                        iconPath = Path.Combine(AppContext.BaseDirectory, line.Substring("icon=".Length).Trim());
                    }
                }
            }

            // --- Setup hidden window for volume mixer session naming ---
            var form = new Form
            {
                Text = appName,
                ShowInTaskbar = false,
                Opacity = 0
            };

            if (File.Exists(iconPath))
            {
                try
                {
                    form.Icon = new Icon(iconPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Could not load icon '{iconPath}': {ex.Message}");
                }
            }

            form.Load += (s, e) => form.Hide();

            var uiThread = new System.Threading.Thread(() =>
            {
                Application.Run(form);
            })
            {
                IsBackground = true
            };
            uiThread.SetApartmentState(System.Threading.ApartmentState.STA);
            uiThread.Start();

            Console.WriteLine($"[INFO] App running as '{appName}' in volume mixer.");

            // --- Load and start agents ---
            if (!Directory.Exists(BaseSoundFolder))
            {
                Console.WriteLine($"[ERROR] Sound folder not found: {BaseSoundFolder}");
                return;
            }

            Console.WriteLine($"[START] Scanning agents in {BaseSoundFolder}...");

            var agentDirs = Directory.GetDirectories(BaseSoundFolder);

            if (agentDirs.Length == 0)
            {
                Console.WriteLine($"[WARN] No agent folders found in {BaseSoundFolder}.");
            }

            foreach (var dir in agentDirs)
            {
                try
                {
                    var agent = new SoundAgent(dir);
                    Task.Run(() => agent.RunLoop());
                    Console.WriteLine($"[OK] Started agent in {Path.GetFileName(dir)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Could not start agent in {dir}: {ex.Message}");
                }
            }

            Console.WriteLine("[READY] All agents initialized. Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
