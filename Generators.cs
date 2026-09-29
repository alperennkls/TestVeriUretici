using System;
using System.Text;

namespace TestVeriUretici
{
    /// <summary>
    /// Generates and validates Turkish identifiers for test data: TC Kimlik No, VKN and TR IBAN.
    /// Generated values satisfy the official check-digit rules; everything else is random.
    /// </summary>
    public static class Generators
    {
        private static readonly Random Rng = new Random();

        // EFT codes of common banks; a TR IBAN carries the bank code as 5 digits right after the check digits
        private static readonly string[] BankCodes =
        {
            "00010", // Ziraat Bankası
            "00012", // Halkbank
            "00015", // VakıfBank
            "00032", // TEB
            "00046", // Akbank
            "00062", // Garanti BBVA
            "00064", // İş Bankası
            "00067", // Yapı Kredi
            "00111", // QNB
            "00134"  // DenizBank
        };

        /// <summary>11 digits, first digit not 0, digits 10 and 11 are check digits.</summary>
        public static string Tc()
        {
            int[] d = RandomDigits(11);
            d[0] = Rng.Next(1, 10);
            d[9] = TcTenthDigit(d);
            d[10] = TcEleventhDigit(d);
            return Join(d);
        }

        /// <summary>10 digits, the last one is the GİB check digit. First digit kept non-zero so Excel doesn't strip it.</summary>
        public static string Vkn()
        {
            int[] d = RandomDigits(10);
            d[0] = Rng.Next(1, 10);
            d[9] = VknCheckDigit(d);
            return Join(d);
        }

        /// <summary>TR + 2 check digits + 5-digit bank code + reserve digit 0 + 16-digit account number, no spaces.</summary>
        public static string Iban()
        {
            string bban = BankCodes[Rng.Next(BankCodes.Length)] + "0" + Join(RandomDigits(16));
            int check = 98 - Mod97(bban + "TR00");
            return "TR" + check.ToString("00") + bban;
        }

        public static bool IsValidTc(string value)
        {
            int[] d = ParseDigits(value, 11);
            return d != null && d[0] != 0 && d[9] == TcTenthDigit(d) && d[10] == TcEleventhDigit(d);
        }

        public static bool IsValidVkn(string value)
        {
            int[] d = ParseDigits(value, 10);
            return d != null && d[9] == VknCheckDigit(d);
        }

        public static bool IsValidIban(string value)
        {
            return value != null
                && value.StartsWith("TR", StringComparison.Ordinal)
                && ParseDigits(value.Substring(2), 24) != null
                && Mod97(value.Substring(4) + value.Substring(0, 4)) == 1;
        }

        // (7 x digits 1,3,5,7,9 - digits 2,4,6,8) mod 10
        private static int TcTenthDigit(int[] d)
        {
            int odd = d[0] + d[2] + d[4] + d[6] + d[8];
            int even = d[1] + d[3] + d[5] + d[7];
            return ((odd * 7 - even) % 10 + 10) % 10;
        }

        // Sum of the first 10 digits mod 10
        private static int TcEleventhDigit(int[] d)
        {
            int sum = 0;
            for (int i = 0; i < 10; i++) sum += d[i];
            return sum % 10;
        }

        // GİB rule: shift each of the first 9 digits by (9 - index) mod 10 and weight it by 2^(9 - index) mod 9,
        // where a non-zero shifted digit whose weight comes out 0 counts as 9; the check digit completes the sum to a multiple of 10
        private static int VknCheckDigit(int[] d)
        {
            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int shifted = (d[i] + 9 - i) % 10;
                int weighted = shifted * (1 << (9 - i)) % 9;
                if (shifted != 0 && weighted == 0) weighted = 9;
                sum += weighted;
            }
            return (10 - sum % 10) % 10;
        }

        // ISO 7064 MOD 97-10, letters count as two digits (A = 10 ... Z = 35)
        private static int Mod97(string s)
        {
            int rest = 0;
            foreach (char c in s)
                rest = c >= '0' && c <= '9' ? (rest * 10 + (c - '0')) % 97 : (rest * 100 + (c - 'A' + 10)) % 97;
            return rest;
        }

        private static int[] RandomDigits(int count)
        {
            int[] d = new int[count];
            for (int i = 0; i < count; i++) d[i] = Rng.Next(10);
            return d;
        }

        private static int[] ParseDigits(string value, int length)
        {
            if (value == null || value.Length != length) return null;
            int[] d = new int[length];
            for (int i = 0; i < length; i++)
            {
                if (value[i] < '0' || value[i] > '9') return null;
                d[i] = value[i] - '0';
            }
            return d;
        }

        private static string Join(int[] d)
        {
            StringBuilder sb = new StringBuilder(d.Length);
            foreach (int digit in d) sb.Append((char)('0' + digit));
            return sb.ToString();
        }
    }
}
