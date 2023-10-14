//GPL-3.0-only or GPL-3.0-or-later
//Copyright Ján Repka 2023
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;


namespace MC_Server_Manager {

    
    class NewServer {
        public static void Setup() {
            Console.Clear();

            try {
                if (!File.Exists(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt")) {
                    File.Create(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt");
                    Console.WriteLine("Options file was created");
                }
            }
            catch (Exception e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + e);
                Console.ForegroundColor = ConsoleColor.White;
                Console.ReadKey();
            }

            string jdkPath = Environment.CurrentDirectory + @"\jdk-17.0.5\bin\java.exe";
            Console.Clear();
            Console.WriteLine("How much RAM do you want to use for your server?");
            int maxRam = int.Parse(Console.ReadLine());

            try {
                string[] optionstxtLines = File.ReadAllLines(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt");
                bool ramLineFound = false;
                for (int i = 0; i < optionstxtLines.Length; i++) {
                    if (optionstxtLines[i].StartsWith("max_ram=")) {
                        optionstxtLines[i] = "max_ram=" + maxRam;
                        ramLineFound = true;
                        break;
                    }
                }

                if (!ramLineFound) {
                    optionstxtLines = optionstxtLines.Append("max_ram=" + maxRam).ToArray();
                }

                File.WriteAllLines(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt", optionstxtLines);

                Console.WriteLine("Setting was saved to options file");
            }
            catch (Exception e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error" + e);
                Console.ForegroundColor = ConsoleColor.White;
            }
            
            try {
                Console.WriteLine("Starting server");
                Process eulaSetupProcess = new();
                eulaSetupProcess.StartInfo.FileName = Environment.CurrentDirectory + @"\jdk-17.0.5\bin\java.exe";
                eulaSetupProcess.StartInfo.Arguments = "-Xmx" + maxRam + "G -jar paper-1.20.1-196.jar -nogui";
                
                eulaSetupProcess.Start();
                eulaSetupProcess.WaitForExit();
                
            }
            catch(Exception e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error:" + e);
                Console.WriteLine("\nThis can be caused by missing JDK");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\nThis is important step for server setup and setup cant continue...");
                Console.ReadKey();
                Environment.Exit(1);
            }
            Console.WriteLine("\nPress enter to accept Minecraft Eula: https://aka.ms/MinecraftEULA");
            Console.ReadLine();
            try {
                string[] eulaTxtLines = File.ReadAllLines(Environment.CurrentDirectory + @"\eula.txt");
                for (int i = 0; i < eulaTxtLines.Length; i++) {
                    eulaTxtLines[i] = eulaTxtLines[i].Replace("false", "true");
                }
                File.WriteAllLines(Environment.CurrentDirectory + @"\eula.txt", eulaTxtLines);
                Console.WriteLine("Eula accepted");
            }
            catch (Exception e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error" + e.Message);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("You can continue pressing enter...");
                Console.ReadKey();
            }
            
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Server was succefuly created. Press enter to get to main menu...");
            Console.ForegroundColor = ConsoleColor.White;
            Console.ReadKey();
            Program program = new();
            Program.Main();            
        }
    }
    class StartServer {
        public static void Start() {
            Console.Clear();
            int maxRam = 4;
            bool maxRamFounded = false;
            try {
                using (StreamReader reader = new StreamReader(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt")) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.StartsWith("max_ram=")) {
                            maxRam = int.Parse(line.Substring("max_ram=".Length));
                            maxRamFounded = true;
                            break;
                        }
                    }
                }

                if (!maxRamFounded) {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Max ram value wasnt finded please set it in options file.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            catch (IOException e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error:" + e);
                Console.ForegroundColor = ConsoleColor.White;
            }

            Process serverProcess = new();
            serverProcess.StartInfo.FileName = Environment.CurrentDirectory + @"\jdk-17.0.5\bin\java.exe";
            serverProcess.StartInfo.Arguments = "-Xmx" + maxRam + "G -jar paper-1.20.1-196.jar -nogui";

            Console.WriteLine("Starting server");
            serverProcess.Start();
            serverProcess.WaitForExit();

            string AfterServerCommandsPath = "0";
            string AfterServerCommandsArgs = "0";

            try {
                using (StreamReader reader = new StreamReader(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt")) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.StartsWith("after_server_path=")) {
                            AfterServerCommandsPath = line.Substring("after_server_path=".Length);
                            break;
                        }
                    }
                }
                using (StreamReader reader = new StreamReader(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt")) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.StartsWith("after_server_args=")) {
                            AfterServerCommandsArgs = line.Substring("after_server_args=".Length);
                            break;
                        }
                    }
                }

                if (AfterServerCommandsPath != "0") {
                    Console.WriteLine("Executing after server");
                    Process afterServerProcess = new();
                    afterServerProcess.StartInfo.FileName = AfterServerCommandsPath;
                    afterServerProcess.StartInfo.Arguments = AfterServerCommandsArgs;

                    afterServerProcess.Start();
                    afterServerProcess.WaitForExit();

                }
            }
            catch (IOException e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error:" + e);
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Server was stopped. Press enter to go to main menu...");
            Console.ForegroundColor = ConsoleColor.White;
            Console.ReadKey();
            Program.Main();
        }
    }
    class Managment {
        public static void ManagmentMenu() {
            Console.Clear();
            Console.WriteLine("1. Server properties\n2. Plugins\n3. Router settings\n4. After server commands \n0. Go back");

            int mainMenuAns = int.Parse(Console.ReadLine());
            if (mainMenuAns == 1) {
                ServerProperties();
            }
            else if (mainMenuAns == 2) {
                Plugins();
            }
            else if (mainMenuAns == 3) {
                RouterSettings();
            }
            else if (mainMenuAns == 4) {
                AfterServerCommands();
            }
            else {
                Program.Main();
            }
        }
        public static void ServerProperties() {
            Process.Start("notepad.exe", Environment.CurrentDirectory + @"\server.properties");
            ManagmentMenu();
        }
        static void Plugins() {
            Process.Start("explorer.exe", Environment.CurrentDirectory + @"\plugins");
            ManagmentMenu();
        }
        public static IPAddress GetDefaultGateway()
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

        static void OpenUrl(string url)
            {
                try
                {
                    Process.Start(url);
                }
                catch
                {
                    url = url.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }

        public static void RouterSettings() {
            var defaultGateway = GetDefaultGateway();
                OpenUrl(Convert.ToString($"http://{defaultGateway}"));
                ManagmentMenu();
        }
        static void AfterServerCommands() {
            string AfterServerCommandsPath = null;
            bool AfterServerCommandsPathFounded = false;
            try {
                using (StreamReader reader = new StreamReader(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt")) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.StartsWith("after_server_path=")) {
                            AfterServerCommandsPath = line.Substring("after_server_path=".Length);
                            AfterServerCommandsPathFounded = true;
                            Console.WriteLine("after_server_path was founded with value:" + AfterServerCommandsPath);
                            break;
                        }
                    }
                }

                if (!AfterServerCommandsPathFounded) {
                    File.AppendAllText(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt" , "after_server_path=0\n");
                    Console.WriteLine("after_server_path was setted to 0");
                    AfterServerCommands();
                }
            }
            catch (IOException e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error:" + e);
                Console.ForegroundColor = ConsoleColor.White;
                Console.ReadLine();
            }


            string AfterServerCommandsArgs = null;
            bool AfterServerCommandsArgsFounded = false;
            try {
                using (StreamReader reader = new StreamReader(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt")) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.StartsWith("after_server_args=")) {
                            AfterServerCommandsArgs = line.Substring("after_server_args=".Length);
                            AfterServerCommandsArgsFounded = true;
                            Console.WriteLine("after_server_args was founded with value:" + AfterServerCommandsPath);
                            break;
                        }
                    }
                }

                if (!AfterServerCommandsArgsFounded) {
                    File.AppendAllText(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt" , "after_server_args=0\n");
                    Console.WriteLine("after_server_args was setted to 0");
                    AfterServerCommands();
                }
            }
            catch (IOException e) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error:" + e);
                Console.ForegroundColor = ConsoleColor.White;
            }
            Console.Clear();

            if (AfterServerCommandsPath == "0") {
                Console.WriteLine("After server commands path: Not setted");
            }
            else {
                Console.WriteLine("After server commands path: " + AfterServerCommandsPath);
            }
            if (AfterServerCommandsArgs == "0") {
                Console.WriteLine("After server args: Not setted");
            }
            else {
                Console.WriteLine("After server atgs: " + AfterServerCommandsArgs);
            }

            Console.WriteLine("\n1. Edit path\n2. Edit arguments\n0. Go back");
            int AFC_Ans = int.Parse(Console.ReadLine());
            if (AFC_Ans == 1) {
                Console.Clear();
                Console.Write("Write path: ");
                AfterServerCommandsPath = Console.ReadLine();
                try {
                    string[] lines = File.ReadAllLines(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt");
                    for (int i = 0; i < lines.Length; i++) {
                        if (lines[i].StartsWith("after_server_path=")) {
                            lines[i] = "after_server_path=" + AfterServerCommandsPath;
                            break;
                        }
                    }

                    File.WriteAllLines(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt", lines);
                }
                catch (IOException e) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + e);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                ManagmentMenu();
                

            }
            else if (AFC_Ans == 2) {
                Console.Clear();
                Console.Write("Write args: ");
                AfterServerCommandsArgs = Console.ReadLine();
                try {
                    string[] lines = File.ReadAllLines(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt");
                    for (int i = 0; i < lines.Length; i++) {
                        if (lines[i].StartsWith("after_server_args=")) {
                            lines[i] = "after_server_args=" + AfterServerCommandsArgs;
                            break;
                        }
                    }

                    File.WriteAllLines(Environment.CurrentDirectory + @"\MC_Server_Manager_options.txt", lines);
                }
                catch (IOException e) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + e);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                ManagmentMenu();
            }
            else {
                ManagmentMenu();
            }
        }
    }
    class Program {
        public static void Main() {
            Console.Clear();
            NewServer newServer = new();
            StartServer startServer = new();
            Managment managment = new();

            Console.WriteLine("1. New server\n2. Start server\n3. Managment");
            string mainMenuAns = Console.ReadLine();
            

            if (mainMenuAns == "1") {

                NewServer.Setup();
            }
            else if (mainMenuAns == "2") {
                StartServer.Start();
            }
            else if (mainMenuAns == "3") {
                Managment.ManagmentMenu();
            }
        }
        
    }
}
