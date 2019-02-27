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
    }
}
