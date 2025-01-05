using Microsoft.Win32;
using System.Diagnostics.Eventing.Reader;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.Diagnostics;
using System.ComponentModel;

namespace OXYGEN_PC_TWEAKER
{
    public partial class Form1 : Form
    {
        private const string GameBarRegistryPath = @"SOFTWARE\Microsoft\GameBar";
        private const string AutoGameModeEnabledValueName = "AutoGameModeEnabled";

        [DllImport("user32.dll")]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        public const uint SPI_SETKEYBOARDDELAY = 0x0017;
        public const uint SPIF_UPDATEINIFILE = 0x01;
        public const uint SPIF_SENDCHANGE = 0x02;


        static void ReduceKeyboardDelay()
        {
            Console.WriteLine("Reducing keyboard delay...");
            SystemParametersInfo(SPI_SETKEYBOARDDELAY, 0, IntPtr.Zero, 0x01 | SPIF_SENDCHANGE);
        }

        static void IncreaseKeyboardRepeatRate()
        {
            Console.WriteLine("Increasing keyboard repeat rate...");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard", true))
            {
                if (key != null)
                {
                    key.SetValue("KeyboardDelay", "0", RegistryValueKind.String);
                    key.SetValue("KeyboardSpeed", "31", RegistryValueKind.String);
                }
            }
        }

        static void DisableFilterKeys()
        {
            Console.WriteLine("Disabling filter keys...");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\Keyboard Response", true))
            {
                if (key != null)
                {
                    key.SetValue("Flags", "122", RegistryValueKind.String);
                    key.SetValue("AutoRepeatDelay", "0", RegistryValueKind.String);
                    key.SetValue("AutoRepeatRate", "0", RegistryValueKind.String);
                    key.SetValue("BounceTime", "0", RegistryValueKind.String);
                    key.SetValue("DelayBeforeAcceptance", "0", RegistryValueKind.String);
                }
            }
        }


        static void DisableNaglesAlgorithm()
        {
            Console.WriteLine("Disabling Nagle's Algorithm...");
            RunCommand("netsh int tcp set global nagle=disabled");
        }

        static void IncreaseTCPWindowSize()
        {
            Console.WriteLine("Increasing TCP Window Size...");
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", true))
            {
                if (key != null)
                {
                    key.SetValue("TcpWindowSize", 65535, RegistryValueKind.DWord);
                    key.SetValue("GlobalMaxTcpWindowSize", 65535, RegistryValueKind.DWord);
                }
            }
        }

        static void SetDNSServers()
        {
            Console.WriteLine("Setting DNS servers to Google's public DNS...");
            RunCommand("netsh interface ip set dns name=\"Ethernet\" static 8.8.8.8");
            RunCommand("netsh interface ip add dns name=\"Ethernet\" 8.8.4.4 index=2");
        }

        static void FlushDNSCache()
        {
            Console.WriteLine("Flushing DNS cache...");
            RunCommand("ipconfig /flushdns");
        }



        string[] servicesToDisable = { "wuauserv", "WaaSMedicSvc", "UsoSvc" };

        static void DisableService(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }

                    sc.Close();
                }

                using (ServiceController sc = new ServiceController(serviceName))
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                System.Diagnostics.Process.Start("sc.exe", $"config {serviceName} start= disabled");
                Console.WriteLine($"Disabled {serviceName} service");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to disable {serviceName}: {ex.Message}");
            }
        }
        static void DisableServices()
        {
            Console.WriteLine("Disabling unnecessary services...");
            string[] servicesToDisable = { "DiagTrack", "WSearch", "SysMain", "Themes" };

            foreach (string service in servicesToDisable)
            {
                try
                {
                    ServiceController sc = new ServiceController(service);
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                    }
                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                    sc.Close();
                    RunCommand($"sc config {service} start= disabled");
                    Console.WriteLine($"Disabled {service} service");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to disable {service}: {ex.Message}");
                }
            }
        }

        static void DisableFullscreenOptimizations()
        {
            Console.WriteLine("Disabling fullscreen optimizations...");
            RunCommand("reg add \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehavior /t REG_DWORD /d 2 /f");
        }

        static void SetGPUPreference()
        {
            Console.WriteLine("Setting GPU preference to high performance...");
            RunCommand("reg add \"HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences\" /v DirectXUserGlobalSettings /t REG_SZ /d \"GpuPreference=2;\" /f");
        }

        static void ClearShaderCache()
        {
            Console.WriteLine("Clearing GPU shader cache...");
            string shaderCachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\NVIDIA\\GLCache";
            try
            {
                if (System.IO.Directory.Exists(shaderCachePath))
                {
                    System.IO.Directory.Delete(shaderCachePath, true);
                    Console.WriteLine("Shader cache cleared successfully.");
                }
                else
                {
                    Console.WriteLine("Shader cache directory not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear shader cache: {ex.Message}");
            }
        }
        static void SetHighPerformancePower()
        {
            Console.WriteLine("Setting high performance power plan...");
            RunCommand("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        }

        static void DisableWindowsDefender()
        {
            Console.WriteLine("Disabling Windows Defender real-time protection...");
            RunCommand("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\" /v DisableAntiSpyware /t REG_DWORD /d 1 /f");
        }

        static void RunCommand(string command)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C " + command;
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        static extern uint BluetoothEnableDiscovery(IntPtr hRadio, bool fEnabled);

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

        [StructLayout(LayoutKind.Sequential)]

        struct BLUETOOTH_FIND_RADIO_PARAMS
        {
            public uint dwSize;

        }


        private void ClearSQLiteTable(string dbPath, string tableName)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DELETE FROM {tableName};";
                    command.ExecuteNonQuery();
                }
            }
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_SENDWININICHANGE = 0x02;


        private static readonly string[] startupPaths = new string[]
        {
        @"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
        @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        public static void DisableAllStartupApps()
        {
            try
            {
                foreach (string path in startupPaths)
                {
                    DisableStartupAppsInPath(Registry.CurrentUser, path);
                    DisableStartupAppsInPath(Registry.LocalMachine, path);
                }
                Console.WriteLine("All startup apps have been disabled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static void DisableStartupAppsInPath(RegistryKey hive, string path)
        {
            using (RegistryKey key = hive.OpenSubKey(path, true))
            {
                if (key != null)
                {
                    string[] valueNames = key.GetValueNames();
                    foreach (string valueName in valueNames)
                    {
                        key.DeleteValue(valueName, false);
                        Console.WriteLine($"Disabled startup app: {valueName}");
                    }
                }
            }
        }

        private Point mouseOffset;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            panel3.Show();

            label3.Text = "";

            panel3.Width = 16;

            if (checkBox1.Checked)
            {
                string tempPath = Path.GetTempPath();

                try
                {
                    DirectoryInfo di = new DirectoryInfo(tempPath);

                    foreach (FileInfo file in di.GetFiles())
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception ex)
                        {

                        }
                    }

                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        try
                        {
                            dir.Delete(true);
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                }
                catch (Exception ex)
                {

                }

            }

            if (checkBox2.Checked)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(GameBarRegistryPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(AutoGameModeEnabledValueName, 1, RegistryValueKind.DWord);
                        }
                        else
                        {

                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox3.Checked)
            {
                try
                {
                    uint result = SHEmptyRecycleBin(IntPtr.Zero, null,
                        (uint)(RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND));

                    if (result == 0)
                    {

                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox4.Checked)
            {
                string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                try
                {
                    string[] files = Directory.GetFiles(downloadsPath);
                    int deletedCount = 0;

                    foreach (string file in files)
                    {
                        File.Delete(file);
                        deletedCount++;

                    }


                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox5.Checked)
            {
                try
                {
                    BLUETOOTH_FIND_RADIO_PARAMS btfrp = new BLUETOOTH_FIND_RADIO_PARAMS();
                    btfrp.dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS));
                    IntPtr hRadio;

                    if (BluetoothFindFirstRadio(ref btfrp, out hRadio) != IntPtr.Zero)
                    {
                        uint result = BluetoothEnableDiscovery(hRadio, false);
                        if (result == 0)
                        {

                        }
                        else
                        {

                        }
                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox6.Checked)
            {

                string picturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

                try
                {
                    int deletedCount = 0;

                    // Delete files
                    foreach (string file in Directory.EnumerateFiles(picturesPath, "*", SearchOption.AllDirectories))
                    {
                        File.Delete(file);
                        deletedCount++;

                    }

                    // Delete subdirectories
                    foreach (string dir in Directory.EnumerateDirectories(picturesPath))
                    {
                        Directory.Delete(dir, true);
                        deletedCount++;

                    }

                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox7.Checked)
            {
                try
                {
                    // Create a solid black image
                    using (Bitmap bmp = new Bitmap(1, 1))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.Black);
                        }

                        // Save the image to a temporary file
                        string tempPath = Path.Combine(Path.GetTempPath(), "black_background.bmp");
                        bmp.Save(tempPath, ImageFormat.Bmp);

                        // Set the wallpaper
                        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, 0x01 | SPIF_SENDWININICHANGE);


                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox14.Checked)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);

                        }
                        else
                        {

                        }
                    }

                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("MinAnimate", "0", RegistryValueKind.String);

                        }
                        else
                        {

                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox13.Checked)
            {
                try
                {
                    ServiceController sc = new ServiceController("WpnUserService");
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (checkBox12.Checked)
            {
                DisableAllStartupApps();
            }

            if (checkBox11.Checked)
            {


                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("MouseSpeed", "0");
                            key.SetValue("MouseThreshold1", "0");
                            key.SetValue("MouseThreshold2", "0");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disabling mouse acceleration: {ex.Message}");
                }


            }

            if (checkBox10.Checked)
            {
                IncreaseKeyboardRepeatRate();
            }


            if (checkBox9.Checked)
            {
                DisableWindowsDefender();
            }

            if (checkBox8.Checked)
            {
                SetHighPerformancePower();
            }

            if (checkBox18.Checked)
            {
                DisableServices();
            }

            if (checkBox17.Checked)
            {
                ClearShaderCache();
            }

            if (checkBox17.Checked)
            {
                DisableFullscreenOptimizations();
            }

            if (checkBox36.Checked)
            {
                SetGPUPreference();
            }

            if (checkBox35.Checked)
            {
                foreach (string service in servicesToDisable)
                {
                    DisableService(service);
                }
            }

            if (checkBox34.Checked)
            {
                SetDNSServers();
            }

            if (checkBox33.Checked)
            {
                FlushDNSCache();
            }

            if (checkBox32.Checked)
            {
                DisableNaglesAlgorithm();
            }


            if (checkBox31.Checked)
            {
                ReduceKeyboardDelay();
            }

            if (checkBox30.Checked)
            {
                DisableFilterKeys();
            }

            label3.Text = "Tweaks Ran Enjoy 😊";

            panel3.Width = 1384;

            MessageBox.Show("Tweeks Done");
            panel3.Hide();
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePos = Control.MousePosition;
                mousePos.Offset(mouseOffset.X, mouseOffset.Y);
                this.Location = mousePos;
            }
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            mouseOffset = new Point(-e.X, -e.Y);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            panel3.Hide();
        }

        private void label4_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void label5_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            
        }
    }
}

