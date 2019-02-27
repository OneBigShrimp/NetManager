using System;
using System.Collections.Generic;

namespace MyNetManager
{
    public class Utils
    {
        private static readonly DateTime DateStart = new DateTime(1970, 1, 1);

        public static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - DateStart).TotalMilliseconds;
        }


        static uint[] cryptTable = new uint[0x500];
        public static int HashString(string lpszString)
        {
            lpszString = lpszString.ToUpper();
            int index = 0;
            uint seed1 = 0x7FED7FED, seed2 = 0xEEEEEEEE;
            int ch;

            while (index < lpszString.Length)
            {
                char key = lpszString[index++];

                seed1 = cryptTable[(1 << 8) + key] ^ (seed1 + seed2);
                seed2 = key + seed1 + seed2 + (seed2 << 5) + 3;
            }
            return (int)seed1;
        }

    }
}
