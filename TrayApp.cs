using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TestVeriUretici
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            // Build steps: build.ps1 embeds this .ico as the .exe icon and packs these images into the Store package
            if (args.Length == 2 && args[0] == "--make-icon")
            {
                AppIcon.SaveIco(args[1]);
                return;
            }
            if (args.Length == 2 && args[0] == "--make-store-assets")
            {
                AppIcon.SaveStoreAssets(args[1]);
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
                    Application.Run(new TrayContext(AutoStart.WasStartedAtSignIn(args)));
                else
                    Application.Run(new Toast("Test Veri Üretici zaten çalışıyor", "Saatin yanındaki ID ikonuna sağ tıkla", false, 4000));
            }
        }
    }

    /// <summary>Tray icon whose right-click menu generates a value and copies it to the clipboard.</summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly MenuItem startupItem;

        public TrayContext(bool startedAtSignIn)
        {
            startupItem = new MenuItem("Windows ile başlat", ToggleStartup);

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
            ShowStartupState();

            // The Store package cannot move its icon (its registry writes stay private), so only the exe pins itself
            if (!AppPackage.IsPackaged) TrayPin.PinWhenRegistered();
            // Opened by hand: say where it went. Started with Windows: stay quiet.
            if (!startedAtSignIn)
                Toast.Popup("Test Veri Üretici çalışıyor", WhereIsTheIcon(), true, 4000);
        }

        private static string WhereIsTheIcon()
        {
            return AppPackage.IsPackaged && !TrayPin.IsPromoted()
                ? "İkon ^ altında, saatin yanına sürükle"
                : "Saatin yanındaki ID ikonuna sağ tıkla";
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

        // Reading the Store package's startup task is asynchronous; the check mark appears once it is known
        private async void ShowStartupState()
        {
            try
            {
                startupItem.Checked = await AutoStart.GetStateAsync() == AutoStartState.Enabled;
            }
            catch (Exception)
            {
                startupItem.Checked = false;
            }
        }

        private async void ToggleStartup(object sender, EventArgs e)
        {
            bool enable = !startupItem.Checked;
            try
            {
                AutoStartState state = await AutoStart.SetEnabledAsync(enable);
                startupItem.Checked = state == AutoStartState.Enabled;
                if (!enable || state == AutoStartState.Enabled) return;
                if (state == AutoStartState.DisabledByUser)
                {
                    // Switched off in Settings or Task Manager: Windows lets only the user switch it back on there
                    Toast.Popup("Windows ile başlat Ayarlar'da kapalı", "Açılan sayfada Test Veri Üretici'yi aç", false, 5000);
                    AutoStart.OpenStartupSettings();
                }
                else
                {
                    Toast.Popup("Windows ile başlat açılamadı", "Bir ilke engelliyor olabilir", false, 4000);
                }
            }
            catch (Exception ex)
            {
                Toast.Popup("Ayar kaydedilemedi", ex.Message, false);
            }
        }
    }
}
