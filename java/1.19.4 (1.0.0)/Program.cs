using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.Zip;
using System.Threading;
using NetFwTypeLib;
using System.Security.Principal;
using System.Security.Policy;
using System.Net;
using System.Configuration;

namespace ConsoleApp2

{
    class Managment
    {
        public void Menu()
        {
            Console.Clear();
            Console.WriteLine("What do you want to manage? \n1. Server properties\n2. after server commands\n0. go back");
            int ans = int.Parse(Console.ReadLine());
            if (ans == 1)
            {
                Properties();
            }
            else if (ans == 2)
            {
                AfterServer();

            }
            else if (ans == 3)
            {
                Plugins();
            }
            else if (ans == 0)
            {
                Program.Main();
            }

            void Properties()
            {
                string currentPath = Environment.CurrentDirectory;
                string propertiesPath = $@"{currentPath}\server.properties";
                string[] propContent = new string[0];
                string valueName;
                string value;

                while (true)
                {
                    Console.WriteLine("Attempting to read server.properties content");

                    Console.Clear();
                    try
                    {
                        propContent = File.ReadAllLines(propertiesPath);
                        Console.WriteLine(string.Join(Environment.NewLine, propContent));
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine("File not found.");
                        Console.ReadKey();
                        Environment.Exit(30);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("An error occurred while reading the file.");
                        Console.ReadKey();
                        Environment.Exit(31);
                    }

                    Console.WriteLine(propContent);
                    Console.WriteLine("Write name of value you want to edit (if you want to exit editor write exit):");
                    valueName = Console.ReadLine();
                    if (valueName == "exit")
                    {
                        break;
                    }
                    Console.WriteLine($"Set new value for {valueName}=");
                    value = Console.ReadLine();

                    try
                    {
                        for (int i = 0; i < propContent.Length; i++)
                        {
                            // Check if the line starts with the specified property name
                            if (propContent[i].StartsWith(valueName + "="))
                            {
                                // Replace the line with the new value
                                propContent[i] = $"{valueName}={value}";
                                File.WriteAllLines("server.properties", propContent);
                                break; // Exit the loop since the line has been replaced
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("Error:" + exc);
                    }

                }
                Console.Clear();
                Menu();
            }

            void AfterServer()
            {
                Console.WriteLine("Checking if afterserver.cmd exists");
                string afterServerPath = "afterserver.cmd";
                try
                {
                    if (!File.Exists(afterServerPath))
                    {
                        // Create the file if it doesn't exist
                        File.WriteAllText(afterServerPath, string.Empty);
                        Console.WriteLine("afterserver file was created");
                    }
                    else { Console.WriteLine("File already exists"); }
                }
                catch (Exception exc)
                {
                    Console.WriteLine("Error: " + exc);

                }
                try
                {
                    Console.WriteLine("Opening afterserver file");
                    Process afterserverEdit = Process.Start("notepad.exe", afterServerPath);
                    afterserverEdit.WaitForExit();
                    
                }
                catch (Exception exc)
                {
                    Console.WriteLine("Error: " + exc);
                }
                Menu();
            }
            void Plugins()
            {
                Console.Clear();
                Console.WriteLine("This is plugin folder. There you can add plugins and delete it from your server.\nPress any key top open him.");
                try
                {
                    Process pluginsFolder = Process.Start("explorer.exe", $@"{Environment.CurrentDirectory}\plugins");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error:" + e.Message);
                    Console.Write("Press any key to continue");
                    Console.ReadKey();
                    Menu();
                }
            }
        }
        internal class Program
        {
            public static void Main()
            {
                Console.Clear ();
                Console.WriteLine("Which action you want to do?\n1.Setup new server\n2.Run existing server\n3.Server managment");
                string ans = Console.ReadLine();
                int maxRam;
                string zipPath = "jdk-17.0.5.zip";
                string currentPath = AppDomain.CurrentDomain.BaseDirectory;
                if (ans == "2")
                {
                    Run();
                }
                if (ans == "3")
                {
                    Managment managment = new Managment();
                    managment.Menu();
                }
                if (ans == "1")
                {
                    WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    if (!isAdmin)
                    {
                        Console.WriteLine("Error: Creating new server require administrator privilegs.");
                        Console.ReadKey();
                        Environment.Exit(4);
                    }

                    string extractPath = $"{currentPath}jdk";
                    Console.WriteLine("How much RAM do you want to allow use for server (in GB)?");
                    string ramAns = Console.ReadLine();
                    maxRam = Convert.ToInt32(ramAns);
                    Directory.CreateDirectory(extractPath);
                    Console.WriteLine("JDK directory created");
                    using (ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipPath)))
                    {
                        ZipEntry entry;
                        while ((entry = zipStream.GetNextEntry()) != null)
                        {
                            string entryPath = Path.Combine(extractPath, entry.Name);
                            if (entry.IsDirectory)
                            {
                                Directory.CreateDirectory(entryPath);
                            }
                            else
                            {
                                using (FileStream entryStream = File.Create(entryPath))
                                {
                                    byte[] buffer = new byte[4096];
                                    int bytesRead;
                                    while ((bytesRead = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        entryStream.Write(buffer, 0, bytesRead);
                                    }
                                }
                            }
                        }
                        Console.WriteLine("Java extracted");

                        string jdkPath = $@"{Environment.CurrentDirectory}\jdk\jdk-17.0.5\bin\java.exe";
                        string batchPath = Path.Combine(currentPath, "start.cmd");
                        FileStream fileStream = File.Create("start.cmd");
                        Console.WriteLine("start file created");
                        fileStream.Close();
                        File.WriteAllText(batchPath, $"\"{jdkPath}\" -Xmx{maxRam}G -jar paper-1.19.4-538.jar -nogui\npause");
                        currentPath = Environment.CurrentDirectory;

                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = batchPath,
                            UseShellExecute = true
                        };

                        Process process = Process.Start(startInfo);
                        process.WaitForExit();

                        string eulaPath = $@"{currentPath}\eula.txt";
                        string eulaText = File.ReadAllText(eulaPath);
                        Console.WriteLine("Eula: https://www.minecraft.net/en-us/eula");
                        Console.WriteLine("Press enter to accept eula...");
                        Console.ReadLine();
                        eulaText = eulaText.Replace("false", "true");
                        File.WriteAllText(eulaPath, eulaText);
                        Console.WriteLine("Eula accepted");

                        var firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                        var inboundRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
                        inboundRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                        inboundRule.Description = "Allow incoming TCP traffic on port 25565 for MCServer";
                        inboundRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                        inboundRule.Enabled = true;
                        inboundRule.InterfaceTypes = "All";
                        inboundRule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                        inboundRule.LocalPorts = "25565";
                        inboundRule.Name = "MCServer";
                        inboundRule.Profiles = (int)(NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN | NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE | NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC);
                        firewallPolicy.Rules.Add(inboundRule);
                        Console.WriteLine("Added inbound firewall exception");

                        var outboundRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
                        outboundRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                        outboundRule.Description = "Allow outgoing TCP traffic on port 25565 for MCServer";
                        outboundRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                        outboundRule.Enabled = true;
                        outboundRule.InterfaceTypes = "All";
                        outboundRule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                        outboundRule.LocalPorts = "25565";
                        outboundRule.Name = "MCServer";
                        outboundRule.Profiles = (int)(NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN | NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE | NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC);
                        firewallPolicy.Rules.Add(outboundRule);
                        Console.WriteLine("Added outbound firewall expception");

                        Console.WriteLine("Values can be changed even after creating server");
                        Console.WriteLine("");
                        Console.WriteLine("!!! On yes/no question answear true/false !!!");
                        Console.WriteLine("");
                        Console.WriteLine("Which gamemode do you want to set default?");
                        string defaultGamemode = Console.ReadLine();
                        Console.WriteLine("Do you want to allow command blocks?");
                        string commandBlock = Console.ReadLine();
                        Console.WriteLine("Do you want to enable PVP?");
                        string pvp = Console.ReadLine();
                        Console.WriteLine("How many players can play this server?");
                        string maxPlayers = Console.ReadLine();
                        Console.WriteLine("Do you want to make no warez server?");
                        string origoMode = Console.ReadLine();
                        Console.WriteLine("Do you want to server resourcepack?");
                        bool useServerResourcepack = bool.Parse(Console.ReadLine());
                        string resourcepack = "";
                        string resourcepackSHA1 = "";
                        string resourcepackPrompt = "";
                        string resourcepackFore = "";
                        if (useServerResourcepack == true)
                        {
                            Console.WriteLine("Resource pack url:");
                            resourcepack = Console.ReadLine();
                            Console.WriteLine("Resource pack sha1 (if you dont want to use it just keep blank)");
                            resourcepackSHA1 = Console.ReadLine();
                            Console.WriteLine("Do you want to make custom text which will prompt to user when requested to download resourcepack? (JSON formated text, if you dont want to use it just keep blank)");
                            resourcepackPrompt = Console.ReadLine();
                            Console.WriteLine("Do you want to force players to use server resource pack?");
                            resourcepackFore = Console.ReadLine();

                        }
                        Console.WriteLine("Do you want to allow nether?");
                        string allowNehter = Console.ReadLine();
                        Console.WriteLine("Do you want to force default game mode?");
                        string forceGamemode = Console.ReadLine();
                        Console.WriteLine("Do you want to enable whitelist?");
                        string whitelist = Console.ReadLine();
                        Console.WriteLine("Do you want to enable spawning npcs?");
                        string spawnNPCs = Console.ReadLine();
                        Console.WriteLine("Seed for server world (keep blank for random)");
                        string seed = Console.ReadLine();
                        Console.WriteLine("Do you want allow external tools and services to gather information about the server?");
                        string query = Console.ReadLine();
                        Console.WriteLine("Do you want to set custom world generator settings?");
                        bool worldGenAns = bool.Parse(Console.ReadLine());
                        string customGen = "";
                        if (worldGenAns == true)
                        {
                            Console.WriteLine("Write custom generator settings in JSON");
                            customGen = Console.ReadLine();
                            if (string.IsNullOrEmpty(customGen))
                            {
                                customGen = "{}";
                            }
                        }
                        if (worldGenAns == false)
                        {
                            customGen = "{}";
                        }
                        Console.WriteLine("Server MOTD (you can keep blank):");
                        string motd = Console.ReadLine();
                        Console.WriteLine("Set diffuculty (if you want to set hardcore set hard and you will be asked later if you want hardcore):");
                        string difficulty = Console.ReadLine();
                        Console.WriteLine("Set maximal render distance:");
                        string renderDistance = Console.ReadLine();
                        Console.WriteLine("Set maximal simulation distance:");
                        string simulationDistance = Console.ReadLine();
                        Console.WriteLine("Do you want to enable hardcore: ");
                        string hardcore = Console.ReadLine();
                        Console.WriteLine("Do you want to enable animals spawning?");
                        string spawnAnimals = Console.ReadLine();
                        Console.WriteLine("Do you want to enable monsters spawning?");
                        string spawnMonsters = Console.ReadLine();
                        Console.WriteLine("How big spawn protection do you want? (0 for disabled)");
                        string spawnProtection = Console.ReadLine();
                        Console.WriteLine("All settings saved to variables");

                        string propertiesPath = $@"{currentPath}\server.properties";
                        List<string> lines = new List<string>(File.ReadAllLines(propertiesPath));
                        File.WriteAllText(propertiesPath, string.Empty);
                        File.AppendAllLines(propertiesPath, new[] { lines[0], lines[1], lines[21] });
                        string variableData = $"rcon.port=25575\ngamemode={defaultGamemode}\nenable-command-block={commandBlock}\npvp={pvp}\nmax-chained-neighbor-updates=1000000\nnetwork-compression-threshold=256\nmax-tick-time=60000\nmax-players={maxPlayers}\nonline-mode={origoMode}\nresource-pack-prompt={resourcepackPrompt}\nallow-nether={allowNehter}\nhide-online-players=false\nrcon.password=\nforce-gamemode={forceGamemode}\nwhite-list={whitelist}\nspawn-npcs={spawnNPCs}\npreviews-chat=false\nfunction-permission-level=2\ninitial-enabled-packs=vanilla\ntext-filtering-config=\nmax-world-size=29999984\nenable-jmx-monitoring=false\nlevel-seed={seed}\nenable-query={query}\ngenerator-settings={customGen}\nenforce-secure-profile=true\nlevel-name=world\nmotd={motd} Created with server setuper by Dzejno\nquery.port=25565\ngenerate-structures=true\ndifficulty={difficulty}\nrequire-resource-pack={resourcepackFore}\nuse-native-transport=true\nenable-status=true\nallow-flight=false\ninitial-disabled-packs=\nbroadcast-rcon-to-ops=true\nview-distance={renderDistance}\nserver-ip=\nserver-port=25565\nenable-rcon=false\nsync-chunk-writes=true\nop-permission-level=4\nprevent-proxy-connections=false\nresource-pack={resourcepack}\nentity-broadcast-range-percentage=100\nsimulation-distance={simulationDistance}\nplayer-idle-timeout=0\ndebug=false\nrate-limit=0\nhardcore={hardcore}\nbroadcast-console-to-ops=true\nspawn-animals={spawnAnimals}\nspawn-monsters={spawnMonsters}\nenforce-whitelist=false\nspawn-protection={spawnProtection}\nresource-pack-sha1={resourcepackSHA1}\n";
                        File.AppendAllText(propertiesPath, variableData);
                        Console.WriteLine("All settings saved to properties file");

                        Console.WriteLine("");
                        Console.WriteLine("Press any key to start first time server start. After server start stop it with 'stop' command. Then reopen this program and start it with start option.");
                        Console.ReadLine();
                        Console.WriteLine("Server started");

                        ProcessStartInfo startInfoTwo = new ProcessStartInfo
                        {
                            FileName = batchPath,
                            UseShellExecute = true
                        };

                        Process processTwo = Process.Start(startInfo);
                        processTwo.WaitForExit();
                        Console.WriteLine("Server building completed\nNow program will end and you can reopen it and select start server. And try server managment it can be helpful for you i guess :)");
                        Console.ReadLine();
                        Environment.Exit(1);
                    }
                }
                else
                {
                    Environment.Exit(0);
                }
            }

            static void Run()
            {
                string currentPath = AppDomain.CurrentDomain.BaseDirectory;
                string batchPath = Path.Combine(currentPath, "start.cmd");
                Console.WriteLine("Starting server");
                try
                {
                    ProcessStartInfo runServer = new ProcessStartInfo
                    {
                        FileName = batchPath,
                        UseShellExecute = true
                    };

                    Process startServer = Process.Start(runServer);
                    startServer.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while running server occured: " + ex.Message);
                    Console.ReadLine();
                    Environment.Exit(2);
                }
                string afterServerPath = "afterserver.cmd";
                
                try
                {
                    Process afterServer = Process.Start(afterServerPath);
                    afterServer.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("After server commands is empty or broken. If ASC is broken it is propably because of " + ex);
                }
                Main();
            }
        }
    }
}
