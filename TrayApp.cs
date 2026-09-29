using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TestVeriUretici
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            // Build step: build.ps1 embeds this .ico as the .exe icon
            if (args.Length == 2 && args[0] == "--make-icon")
            {
                AppIcon.SaveIco(args[1]);
                return;
            }

            // Crisp menu, icon and notice on scaled displays instead of bitmap-stretched ones
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool firstInstance;
            using (new Mutex(true, @"Local\TestVeriUretici", out firstInstance))
            {
                if (firstInstance)
                    Application.Run(new TrayContext());
                else
                    Application.Run(new Toast("Test Veri Üretici", "Zaten çalışıyor, tepsideki ikona sağ tıkla", false));
            }
        }
    }

    /// <summary>Tray icon whose right-click menu generates a value and copies it to the clipboard.</summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly MenuItem startupItem;

        public TrayContext()
        {
            startupItem = new MenuItem("Windows ile başlat", ToggleStartup);
            startupItem.Checked = AutoStart.IsEnabled;

            tray.Icon = AppIcon.ForTray(SystemInformation.SmallIconSize.Width);
            tray.Text = "Test Veri Üretici";
            tray.ContextMenu = new ContextMenu(new[]
            {
                new MenuItem("TC üret", (s, e) => Copy("TC", Generators.Tc())),
                new MenuItem("VKN üret", (s, e) => Copy("VKN", Generators.Vkn())),
                new MenuItem("IBAN üret", (s, e) => Copy("IBAN", Generators.Iban())),
                new MenuItem("-"),
                startupItem,
                new MenuItem("Çıkış", (s, e) => ExitThread())
            });
            tray.Visible = true;
        }

        protected override void ExitThreadCore()
        {
            tray.Visible = false;
            tray.Icon.Dispose();
            tray.Dispose();
            base.ExitThreadCore();
        }

        private static void Copy(string kind, string value)
        {
            try
            {
                // Retries cover the moment another app holds the clipboard open
                Clipboard.SetDataObject(value, true, 10, 50);
                Toast.Popup(kind + " kopyalandı", value, true);
            }
            catch (ExternalException)
            {
                Toast.Popup("Panoya kopyalanamadı, tekrar dene", value, false);
            }
        }

        private void ToggleStartup(object sender, EventArgs e)
        {
            try
            {
                AutoStart.IsEnabled = !startupItem.Checked;
                startupItem.Checked = AutoStart.IsEnabled;
            }
            catch (Exception ex)
            {
                Toast.Popup("Ayar kaydedilemedi", ex.Message, false);
            }
        }
    }

    /// <summary>"Windows ile başlat": a per-user Run entry pointing at this .exe.</summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TestVeriUretici";

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(ValueName) != null;
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
