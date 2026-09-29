using System.Runtime.InteropServices;
using System.Text;

namespace TestVeriUretici
{
    /// <summary>Whether this process runs from the Microsoft Store package (has package identity) or as the plain GitHub exe.</summary>
    internal static class AppPackage
    {
        private const int AppModelErrorNoPackage = 15700;
        private static readonly bool isPackaged = Detect();

        public static bool IsPackaged
        {
            get { return isPackaged; }
        }

        private static bool Detect()
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
    }
}
