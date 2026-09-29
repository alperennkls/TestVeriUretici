using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace TestVeriUretici
{
    /// <summary>Console test runner for Generators and TrayPin paths; build.ps1 runs it and stops the build if anything fails.</summary>
    internal static class Tests
    {
        private static int failures;

        private static int Main(string[] args)
        {
            string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

            // Published valid samples and near misses
            Expect("TC 10000000146 gecerli", Generators.IsValidTc("10000000146"));
            Expect("TC 12345678950 gecerli", Generators.IsValidTc("12345678950"));
            Expect("TC 10000000147 gecersiz (11. hane)", !Generators.IsValidTc("10000000147"));
            Expect("TC 10000000156 gecersiz (10. hane)", !Generators.IsValidTc("10000000156"));
            Expect("TC 01000000090 gecersiz (0 ile basliyor)", !Generators.IsValidTc("01000000090"));
            Expect("TC 1000000014 gecersiz (10 hane)", !Generators.IsValidTc("1000000014"));
            Expect("TC 1000000014a gecersiz (harf)", !Generators.IsValidTc("1000000014a"));

            Expect("VKN 1234567890 gecerli", Generators.IsValidVkn("1234567890"));
            Expect("VKN 1000036109 gecerli", Generators.IsValidVkn("1000036109"));
            Expect("VKN 1234567891 gecersiz", !Generators.IsValidVkn("1234567891"));
            Expect("VKN 1000036108 gecersiz", !Generators.IsValidVkn("1000036108"));
            Expect("VKN 123456789 gecersiz (9 hane)", !Generators.IsValidVkn("123456789"));

            Expect("IBAN TR330006100519786457841326 gecerli", Generators.IsValidIban("TR330006100519786457841326"));
            Expect("IBAN TR330006100519786457841327 gecersiz", !Generators.IsValidIban("TR330006100519786457841327"));
            Expect("IBAN TR340006100519786457841326 gecersiz", !Generators.IsValidIban("TR340006100519786457841326"));
            Expect("IBAN bosluklu yazim gecersiz", !Generators.IsValidIban("TR33 0006 1005 1978 6457 8413 26"));

            // Every generated value must validate, and changing its last digit must break it
            for (int i = 0; i < 100000; i++)
            {
                RoundTrip("TC", Generators.Tc(), 11, Generators.IsValidTc);
                RoundTrip("VKN", Generators.Vkn(), 10, Generators.IsValidVkn);
                RoundTrip("IBAN", Generators.Iban(), 26, Generators.IsValidIban);
            }

            // Tests.exe always runs as a plain exe, never from the Store package
            Expect("Paketsiz calisma algilaniyor", !AppPackage.IsPackaged);
            Expect("--autostart oturum acilisi sayiliyor", AutoStart.WasStartedAtSignIn(new[] { AutoStart.Flag }));
            Expect("Argumansiz acilis elle acilis sayiliyor", !AutoStart.WasStartedAtSignIn(new string[0]));
            Expect("Windows ile baslat durumu Run kaydini yansitiyor",
                (AutoStart.GetStateAsync().Result == AutoStartState.Enabled) == RunEntryExists());
            // A task switched off in Settings must stay distinguishable from a plain "off" the menu may flip back on
            Expect("Ayarlar'dan kapatilan gorev ayri durum", AutoStart.FromTaskState(StartupTaskState.DisabledByUser) == AutoStartState.DisabledByUser);
            Expect("Ilkeyle acik gorev acik sayiliyor", AutoStart.FromTaskState(StartupTaskState.EnabledByPolicy) == AutoStartState.Enabled);
            Expect("Ilkeyle kapali gorev kapali sayiliyor", AutoStart.FromTaskState(StartupTaskState.DisabledByPolicy) == AutoStartState.Disabled);
            // Tests.exe never shows a tray icon, so it has no entry and counts as hidden (Store version shows the hint)
            Expect("Tepsi kaydi olmayan exe gizli sayiliyor", !TrayPin.IsPromoted());

            // Every image and the StartupTask id in store\AppxManifest.xml must match what the code produces and uses
            string manifest = File.ReadAllText(Path.Combine(root, @"store\AppxManifest.xml"));
            string assets = Path.Combine(Path.GetTempPath(), "TestVeriUretici-store-assets");
            AppIcon.SaveStoreAssets(assets);
            foreach (Match image in Regex.Matches(manifest, @"Assets\\(\w+)\.png"))
                Expect("Manifest gorseli uretiliyor: " + image.Groups[1].Value,
                    Directory.GetFiles(assets, image.Groups[1].Value + ".*.png").Length > 0);
            Expect("Gorev cubugu ikonu (unplated) uretiliyor",
                File.Exists(Path.Combine(assets, "Square44x44Logo.targetsize-24_altform-unplated.png")));
            Expect("Manifest StartupTask kimligi kodla ayni", manifest.Contains("TaskId=\"" + AutoStart.TaskId + "\""));
            Expect("Manifest baslangic gorevi kapali geliyor", manifest.Contains("Enabled=\"false\""));
            Directory.Delete(assets, true);

            // Values must actually be random, not one valid constant
            Expect("TC degerleri farkli", DistinctCount(Generators.Tc) > 990);
            Expect("VKN degerleri farkli", DistinctCount(Generators.Vkn) > 990);
            Expect("IBAN degerleri farkli", DistinctCount(Generators.Iban) > 990);

            // Tray settings store system folders as "{known folder id}\rest" and other paths as-is
            Expect("Program Files yolu cozuluyor",
                TrayPin.ResolveShellPath(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Test\Uygulama.exe")
                    == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Test\Uygulama.exe"));
            Expect("Windows yolu cozuluyor",
                TrayPin.ResolveShellPath(@"{F38BF404-1D43-42F2-9305-67DE0B28FC23}\explorer.exe")
                    == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
            Expect("Normal yol aynen kaliyor",
                TrayPin.ResolveShellPath(@"C:\Users\Test\Downloads\TestVeriUretici.exe") == @"C:\Users\Test\Downloads\TestVeriUretici.exe");
            Expect("Bozuk GUID aynen kaliyor", TrayPin.ResolveShellPath(@"{bozuk}\a.exe") == @"{bozuk}\a.exe");

            Console.WriteLine(failures == 0 ? "Tum testler gecti." : failures + " test basarisiz.");
            return failures == 0 ? 0 : 1;
        }

        private static void Expect(string name, bool condition)
        {
            if (condition) return;
            failures++;
            Console.WriteLine("BASARISIZ: " + name);
        }

        private static void RoundTrip(string kind, string value, int length, Func<string, bool> isValid)
        {
            char last = value[value.Length - 1];
            string broken = value.Substring(0, value.Length - 1) + (char)('0' + (last - '0' + 1) % 10);
            if (value.Length == length && isValid(value) && !isValid(broken)) return;
            if (failures < 10) Console.WriteLine("BASARISIZ: " + kind + " " + value);
            failures++;
        }

        private static bool RunEntryExists()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key != null && key.GetValue("TestVeriUretici") != null;
        }

        private static int DistinctCount(Func<string> generate)
        {
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < 1000; i++) seen.Add(generate());
            return seen.Count;
        }
    }
}
