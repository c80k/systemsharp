using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.DataTypes;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SystemSharp test cases");
            Console.WriteLine();

            try
            {
                Console.WriteLine("Part 1: Basic data structures");
                Console.WriteLine("  testing SystemSharp.Collections.EmilStefanov.DisjointSets");

                SystemSharp.Collections.EmilStefanov.Test.DisjointSetsTester.RunTests();

                Console.WriteLine("  testing fixed point math");

                TestFixPoint.RunTest();

                Console.WriteLine("Part 2: Design analysis and synthesis");

                TestDesign1.RunTest();
                TestRegPipe.RunTest();
                TestAddMul0.RunTest();
                TestAddMul1.RunTest();
                TestAddMul2.RunTest();
                Mod2TestDesign.Run();
                TestConcatTestbench.Run();

                Console.WriteLine("Part 3: Compiler");

                CompilerTest.Testbench.RunTest();

                Console.WriteLine("Part 4: Component tests");

                ALUTestDesign.Run();
                Mod2TestDesign.Run();
                Test_SinCosLUT_Testbench.RunTest();

                Console.WriteLine("Part 5: HLS");

                TestHLS_PortAccess_Testbench.RunTest();
                TestHLS_ALU_Testbench.RunTest();
                TestHLS_FPU_Testbench.RunTest();
                TestHLS_Cordic_Testbench.RunTest();
                TestHLS_CordicSqrt_Testbench.RunTest();
                TestHLS_CFlow_Testbench.RunTest();
                TestHLS_CFlow2_Testbench.RunTest();
                TestHLS_VanDerPol_Testbench.RunTest();
                TestHLSTestbench1.RunTest();
                TestHLS_SFixDiv.RunTest();
                TestHLS_SinCosLUT_Testbench.RunTest();

                Console.WriteLine("Part 6: File writing");
                FileWriterTestbench.RunTest();

                Console.WriteLine();
                Console.WriteLine("Test passed");
            }
            catch (Exception e)
            {
                Console.WriteLine("Test failed: " + e.Message);
            }
        }
    }
}
