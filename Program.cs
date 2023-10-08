using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Principal;

namespace MC_Server_Manager {
    class Utilities {
        public static bool IsAdministrator
        {
            get
            {
                var identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        
    }
    
    class NewServer {
        public static void Setup() {
            Console.Clear();

            Utilities utilities = new();
            bool adminPrivilegs = Utilities.IsAdministrator;

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
            Console.WriteLine("1. Server properties\n2. Plugins\n3. Router settings (no added yet)\n0. Go back\n\nMore managment features will be added in new update");

            int mainMenuAns = int.Parse(Console.ReadLine());
            if (mainMenuAns == 1) {
                ServerProperties();
            }
            else if (mainMenuAns == 2) {
                Plugins();
            }
            else {
                Program.Main();
            }
        }
        static void ServerProperties() {
            Process.Start("notepad.exe", Environment.CurrentDirectory + @"\server.properties");
            ManagmentMenu();
        }
        static void Plugins() {
            Process.Start("explorer.exe", Environment.CurrentDirectory + @"\plugins");
            ManagmentMenu();
        }
        static void RouterSettings() {
            //
        }
    }
    class Program {
        public static void Main() {
            Console.Clear();
            NewServer newServer = new();
            StartServer startServer = new();
            Managment managment = new();

            Console.WriteLine("2.0 PREVIEW 1\n");

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