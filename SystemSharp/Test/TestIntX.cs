using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if USE_INTX

using IntXLib;

namespace Test
{
    class TestIntX
    {
        public static void Test1()
        {
            IntX x = 1;
            for (int i = 0; i < 100000000; i++)
            {
                x <<= 63;
                x >>= 36;
                x >>= 27;
                Debug.Assert(x == 1);
            }
        }

        public static void Test2()
        {
            var t1 = new Task(Test1);
            var t2 = new Task(Test1);
            t1.Start();
            t2.Start();
            Task.WaitAll(t1, t2);
        }
    }
}

#endif