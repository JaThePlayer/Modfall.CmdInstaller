using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Modfall.MiniInstaller
{
    class Program
    {
        public static string Ver = "1.0";

        public static string ExePath = "";
        public static string ExeFileName;
        public static string InstallerPath;

        public static string TargetVersion = "";
        static void Main(string[] args)
        {
            Console.WriteLine($"Modfall installer v{Ver}");
            InstallerPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h":
                        DisplayHelp();
                        break;
                    case "-exe":
                        i++;
                        if (i < args.Length)
                        {
                            ExePath = args[i];
                            Console.WriteLine($"Path to exe file: {ExePath}");
                        }
                        else
                        {
                            Console.WriteLine("The -exe parameter doesn't have a path specified. Usage: -exe \"path/to/towerfall.exe\"");
                        }
                        break;
                    case "-v":
                        i++;
                        TargetVersion = args[i];
                        break;
                }
            }
            if (ExePath == "")
            {
                Console.WriteLine("Please provide the full path to the TowerFall.exe you want to mod.");
                ExePath = Console.ReadLine().Trim('"');
            }
            if (ExePath != "")
            {
                //DownloadNewestVersion();
                GetVersionList();
                PrintVersionList();
                if (TargetVersion == "")
                {
                    Console.WriteLine("Which version do you want to download? Newest is recommended. Type 'n' to get the newest version.");
                getVersion:
                    string input = Console.ReadLine().Trim();
                    if (input.Trim('\'') == "n")
                    {
                        TargetVersion = Versions.Keys.ToArray()[0];
                    }
                    else if (Versions.ContainsKey(input))
                    {
                        TargetVersion = input;
                    }
                    else
                    {
                        goto getVersion;
                    }
                } else if (TargetVersion == "n")
                {
                    TargetVersion = Versions.Keys.ToArray()[0];
                }

                try
                {
                    tempPath = Path.Combine(InstallerPath, "Temp");
                    DownloadVersion(Versions[TargetVersion], "");
                    ExeFileName = Path.GetFileName(ExePath);
                    string origPath = Path.Combine(Path.GetDirectoryName(ExePath), "orig");
                    string backupExeFileName = Path.Combine(origPath, ExeFileName);
                    Console.WriteLine($"Backing up .exe file to {backupExeFileName}");
                    if (!Directory.Exists(origPath))
                    {
                        Directory.CreateDirectory(origPath);
                    }
                    File.Copy(ExePath, backupExeFileName, true);
                    string newExeFileName = Path.Combine(tempPath, ExeFileName);
                    Console.WriteLine($"Copying {ExePath} to {newExeFileName}");
                    File.Copy(ExePath, newExeFileName, true);
                    RunMonomod();
                    string source = Path.Combine(tempPath, $"MMHOOK_{Path.GetFileNameWithoutExtension(ExeFileName)}.dll");
                    string dest = Path.Combine(Path.GetDirectoryName(ExePath), $"MMHOOK_{Path.GetFileNameWithoutExtension(ExeFileName)}.dll");
                    Console.WriteLine($"Copying {source} to {dest}");
                    File.Copy(source, dest, true);
                    source = Path.Combine(tempPath, $"MONOMODDED_{ExeFileName}");
                    dest = Path.Combine(Path.GetDirectoryName(ExePath), $"MONOMODDED_{ExeFileName}");
                    Console.WriteLine($"Copying {source} to {dest}");
                    File.Copy(source, dest, true);

                    Console.WriteLine("Clearing Temp folder...");
                    foreach (string f in Directory.GetFiles(tempPath))
                    {
                        File.Delete(f);
                    }
                    Console.WriteLine($"\nModfall version {TargetVersion} installed!\nPress any key to continue...");
                    Console.ReadLine();
                } catch (Exception e)
                {
                    Console.WriteLine($"{e.Message}: \n{e.StackTrace}");
                    Console.ReadLine();
                }
                
            }

        }

        public static void PrintVersionList()
        {
            Console.WriteLine("Version List:");
            for (int i = 0; i < Versions.Count; i++)
            {
                Console.WriteLine($"{Versions[Versions.Keys.ToArray()[i]].Ver}");
            }
        }

        public static void GetVersionList()
        {
            Versions = new Dictionary<string, Version>();
            string output = ExecuteCommandSilent("curl https://api.github.com/repos/JaThePlayer/Modfall/releases").TrimStart('[').TrimEnd(']');
            string[] releaseUrls = output.Split(new string[] { "\"assets_url\": " }, StringSplitOptions.None);
            string[] releaseTags = output.Split(new string[] { "\"tag_name\": " }, StringSplitOptions.None);
            for (int i = 1; i < releaseUrls.Length; i++)
            {
                releaseUrls[i] = releaseUrls[i].Remove(releaseUrls[i].IndexOf(',')).Trim('"');
                releaseTags[i] = releaseTags[i].Remove(releaseTags[i].IndexOf(',')).Trim('"');
                Versions.Add(releaseTags[i], new Version { Ver = releaseTags[i], DownloadUrl = releaseUrls[i] });
            }
        }
        static string tempPath;
        public static void DownloadVersion(Version version, string path)
        {
            string downloadUrl = ExecuteCommandSilent($"curl {version.DownloadUrl}").Split(new string[] { "\"browser_download_url\": " }, StringSplitOptions.None)[1].Split('}')[0].Trim();
            Console.WriteLine($"Downloading modfall.zip from {downloadUrl}");
            ExecuteCommandSilent($"curl -L -o Modfall{version.Ver}.zip {downloadUrl}");
            
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            } else
            {
                foreach (string f in Directory.GetFiles(tempPath))
                {
                    File.Delete(f);
                }
            }
            ZipFile.ExtractToDirectory(Path.Combine(InstallerPath, $"Modfall{version.Ver}.zip"), tempPath);
            File.Copy(Path.Combine(InstallerPath, "SharpDX.dll"), Path.Combine(tempPath, "SharpDX.dll"));
            File.Copy(Path.Combine(InstallerPath, "SharpDX.DirectInput.dll"), Path.Combine(tempPath, "SharpDX.DirectInput.dll"));
            File.Copy(Path.Combine(InstallerPath, "MonoMod.RuntimeDetour.HookGen.exe"), Path.Combine(tempPath, "MonoMod.RuntimeDetour.HookGen.exe"));
            File.Delete(Path.Combine(InstallerPath, $"Modfall{version.Ver}.zip"));

        }

        public static void RunMonomod()
        {
            ExecuteCommand($"{Path.Combine("Temp", "MonoMod.exe")} {Path.Combine("Temp", ExeFileName)}");
            ExecuteCommand($"{Path.Combine("Temp", "MonoMod.RuntimeDetour.HookGen.exe")} {Path.Combine("Temp", ExeFileName)}");
        }

        public static void DisplayHelp()
        {
            Console.WriteLine("Usage: Modfall.MiniInstaller.exe [parameters]");
            Console.WriteLine("Possible parameters:");
            Console.WriteLine("-h   Display this help message");
            Console.WriteLine("-exe \"path\" The path to the TowerFall.exe file you want to mod");
        }

        static string ExecuteCommandSilent(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            string result = "";
            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) => result += e.Data;
            process.BeginOutputReadLine();
            process.WaitForExit();

            process.Close();

            return result;
        }

        static void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                Console.WriteLine(e.Data);
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                Console.WriteLine(e.Data);
            process.BeginErrorReadLine();

            process.WaitForExit();

            process.Close();
        }

        //public static List<Version> Versions = new List<Version>();
        public static Dictionary<string, Version> Versions = new Dictionary<string, Version>();
        public struct Version
        {
            public string DownloadUrl;
            public string Ver;
        }
    }
}