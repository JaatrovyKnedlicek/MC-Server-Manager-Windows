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
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace ConsoleApp2

{
    class Managment
    {
        public void Menu()
        {
            Console.Clear();
            Console.WriteLine("What do you want to manage? \n1. Server properties\n2. after server commands\n3. Plugins\n4. Router settings\n0. go back");
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
            else if (ans == 4)
            {
                routerSettings();
            }
            else if (ans == 0)
            {
                Program.Main();
            }

            void Properties()
            {
                Process.Start("notepad.exe", $@"{Environment.CurrentDirectory}\server.properties");
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
                Menu();
            }
            IPAddress GetDefaultGateway()
            {
                return NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                    .Select(g => g?.Address)
                    .Where(a => a != null)
                    // .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    // .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
                    .FirstOrDefault();
            }
            void OpenUrl(string url)
            {
                try
                {
                    Process.Start(url);
                }
                catch
                {
                    // hack because of this: https://github.com/dotnet/corefx/issues/10361
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        url = url.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start("xdg-open", url);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Process.Start("open", url);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            void routerSettings()
            {
                var defaultGateway = GetDefaultGateway();
                OpenUrl(Convert.ToString($"http://{defaultGateway}"));
                Menu();
            }
        }
        internal class Program
        {
            public enum FirewallProfiles
            {
                Domain = 1,
                Private = 2,
                Public = 4
            }
            public static void Main()
            {
                Console.Clear();
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

                        //firewall rules                        
                        INetFwRule2 inboundRuleTCP = (INetFwRule2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                        inboundRuleTCP.Enabled = true;
                        inboundRuleTCP.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                        inboundRuleTCP.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                        inboundRuleTCP.LocalPorts = "25565";
                        inboundRuleTCP.Name = "MCServer";
                        inboundRuleTCP.Profiles = (int)(FirewallProfiles.Private | FirewallProfiles.Public);
                        inboundRuleTCP.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;

                        INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                        firewallPolicy.Rules.Add(inboundRuleTCP);

                        //firewall rules                        
                        INetFwRule2 inboundRuleUDP = (INetFwRule2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                        inboundRuleUDP.Enabled = true;
                        inboundRuleUDP.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                        inboundRuleUDP.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;
                        inboundRuleUDP.LocalPorts = "25565";
                        inboundRuleUDP.Name = "MCServer";
                        inboundRuleUDP.Profiles = (int)(FirewallProfiles.Private | FirewallProfiles.Public);
                        inboundRuleUDP.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;

                        INetFwPolicy2 firewallPolicy2 = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                        firewallPolicy.Rules.Add(inboundRuleUDP);


                        //firewall rules                        
                        INetFwRule2 outboundRuleTCP = (INetFwRule2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                        outboundRuleTCP.Enabled = true;
                        outboundRuleTCP.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                        outboundRuleTCP.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                        outboundRuleTCP.LocalPorts = "25565";
                        outboundRuleTCP.Name = "MCServer";
                        outboundRuleTCP.Profiles = (int)(FirewallProfiles.Private | FirewallProfiles.Public);
                        outboundRuleTCP.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;

                        INetFwPolicy2 firewallPolicy3 = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                        firewallPolicy3.Rules.Add(outboundRuleTCP);

                        //firewall rules
                        INetFwRule2 outboundRuleUDP = (INetFwRule2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                        outboundRuleUDP.Enabled = true;
                        outboundRuleUDP.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                        outboundRuleUDP.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;
                        outboundRuleUDP.LocalPorts = "25565";
                        outboundRuleUDP.Name = "MCServer";
                        outboundRuleUDP.Profiles = (int)(FirewallProfiles.Private | FirewallProfiles.Public);
                        outboundRuleUDP.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;

                        INetFwPolicy2 firewallPolicy4 = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                        firewallPolicy4.Rules.Add(outboundRuleUDP);

                        Console.WriteLine("Firewall rules have been saved");

                        Console.WriteLine("Do you want to use default server.properties values?");
                        ans = Console.ReadLine();
                        if (ans == "yes")
                        {
                            return;
                        }
                        else if (ans == "no")
                        {
                            Process.Start("notepad.exe", $@"{Environment.CurrentDirectory}\server.properties");
                        }

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
