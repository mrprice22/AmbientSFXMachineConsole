using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientAgents
{
    class Program
    {
        static readonly string BaseSoundFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snd");

        static void Main(string[] args)
        {
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
