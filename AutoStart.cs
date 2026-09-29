using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;

namespace TestVeriUretici
{
    internal enum AutoStartState { Disabled, Enabled, DisabledByUser }

    /// <summary>
    /// "Windows ile başlat", only ever turned on from the menu. The GitHub exe uses a per-user Run entry. The Store
    /// package cannot (its registry writes go to a private copy Windows never reads), so it uses the StartupTask in
    /// store\AppxManifest.xml, which the user also sees and controls in Settings > Apps > Startup.
    /// </summary>
    internal static class AutoStart
    {
        /// <summary>Added to the Run entry so a start at sign-in shows no notice.</summary>
        public const string Flag = "--autostart";

        /// <summary>uap5:StartupTask TaskId in store\AppxManifest.xml.</summary>
        internal const string TaskId = "TestVeriUretici";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TestVeriUretici";

        public static bool WasStartedAtSignIn(string[] args)
        {
            return AppPackage.IsPackaged ? ActivatedByStartupTask() : Array.IndexOf(args, Flag) >= 0;
        }

        public static Task<AutoStartState> GetStateAsync()
        {
            return AppPackage.IsPackaged ? GetTaskStateAsync() : Task.FromResult(RunEntryState());
        }

        public static Task<AutoStartState> SetEnabledAsync(bool enabled)
        {
            if (AppPackage.IsPackaged) return SetTaskEnabledAsync(enabled);
            SetRunEntry(enabled);
            return Task.FromResult(RunEntryState());
        }

        /// <summary>Settings > Apps > Startup: the only place a startup task the user switched off can be switched back on.</summary>
        public static void OpenStartupSettings()
        {
            Process.Start("ms-settings:startupapps");
        }

        internal static AutoStartState FromTaskState(StartupTaskState state)
        {
            switch (state)
            {
                case StartupTaskState.Enabled:
                case StartupTaskState.EnabledByPolicy:
                    return AutoStartState.Enabled;
                case StartupTaskState.DisabledByUser:
                    return AutoStartState.DisabledByUser;
                default:
                    return AutoStartState.Disabled;
            }
        }

        private static AutoStartState RunEntryState()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(ValueName) != null ? AutoStartState.Enabled : AutoStartState.Disabled;
        }

        private static void SetRunEntry(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" " + Flag);
                else key.DeleteValue(ValueName, false);
            }
        }

        // The WinRT calls below only work with package identity; unpackaged, Windows rejects them

        private static async Task<AutoStartState> GetTaskStateAsync()
        {
            StartupTask task = await Await(StartupTask.GetAsync(TaskId));
            return FromTaskState(task.State);
        }

        private static async Task<AutoStartState> SetTaskEnabledAsync(bool enabled)
        {
            StartupTask task = await Await(StartupTask.GetAsync(TaskId));
            if (!enabled)
            {
                task.Disable();
                return FromTaskState(task.State);
            }
            // Packaged desktop apps get no consent dialog; a task the user switched off in Settings stays off
            return FromTaskState(await Await(task.RequestEnableAsync()));
        }

        private static bool ActivatedByStartupTask()
        {
            try
            {
                IActivatedEventArgs activation = AppInstance.GetActivatedEventArgs();
                return activation != null && activation.Kind == ActivationKind.StartupTask;
            }
            catch (Exception)
            {
                return false; // no activation info: treat it as an ordinary start and show the notice
            }
        }

        // The Windows-bundled compiler only sees the per-namespace metadata in System32\WinMetadata, which the
        // framework's AsTask() extensions cannot bind to, so awaiting a WinRT operation goes through this adapter
        private static Task<T> Await<T>(IAsyncOperation<T> operation)
        {
            TaskCompletionSource<T> done = new TaskCompletionSource<T>();
            operation.Completed = delegate (IAsyncOperation<T> info, AsyncStatus status)
            {
                if (status == AsyncStatus.Completed) done.TrySetResult(info.GetResults());
                else if (status == AsyncStatus.Canceled) done.TrySetCanceled();
                else done.TrySetException(info.ErrorCode);
            };
            return done.Task;
        }
    }
}
