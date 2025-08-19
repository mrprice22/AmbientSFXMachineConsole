using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms; //for making a blank window to control name of program in volume mixer or alt+tab in windows
using System.Drawing; //for the icon display 

namespace AmbientAgents
{
    class Program
    {
        static readonly string BaseSoundFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snd");

        
        [STAThread]
        static void Main(string[] args)
        {
            // Read appName from config
            string appName = "Default Sound Agent";
            string iconPath = Path.Combine(AppContext.BaseDirectory, "agent.ico");

            string configPath = Path.Combine(AppContext.BaseDirectory, "appSettings.config");
            if (File.Exists(configPath))
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
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

            // Create hidden window for audio session naming
            var form = new Form();
            form.Text = appName;
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            if (File.Exists(iconPath))
                form.Icon = new Icon(iconPath);

            form.Load += (s, e) => form.Hide();

            // Start the hidden form in a new thread
            System.Threading.Thread t = new System.Threading.Thread(() =>
            {
                Application.Run(form);
            });
            t.IsBackground = true;
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();

            Console.WriteLine($"[INFO] App running as '{appName}' in volume mixer.");
            
            // ...initialize agents, play sounds, etc.
            if (!Directory.Exists(BaseSoundFolder))
            {
                Console.WriteLine($"Sound folder not found: {BaseSoundFolder}");
                return;
            }

            Console.WriteLine($"[START] Scanning agents in {BaseSoundFolder}...");

            var agentDirs = Directory.GetDirectories(BaseSoundFolder);

            foreach (var dir in agentDirs)
            {
                try
                {
                    var agent = new SoundAgent(dir);
                    Task.Run(() => agent.RunLoop());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Could not start agent in {dir}: {ex.Message}");
                }
            }

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
