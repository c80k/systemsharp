/** System# example: floating point FFT
 * 
 * This example demonstrates a Fast Fourier Transform (FFT) simulation.
 * 
 * It shows that the System# concepts are very similar to SystemC: The given
 * example is actually a port of the following SystemC example:
 *   examples/sysc/fft/fft_flpt
 * 
 * We tried to keep the implementation as close as possible to the original 
 * SystemC example. You are encouraged to compare both implementations to find
 * out the similarities and differences.
 *    */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;

namespace Example_FFT_FloatingPoint
{
    static class Log
    {
        public static bool Verbose { get; set; }

        public static void WriteLine(string text)
        {
            if (Verbose)
                Console.WriteLine(text);
        }

        public static void WriteLine()
        {
            if (Verbose)
                Console.WriteLine();
        }
    }

    class FFT : Component
    {
        public In<float> in_real { private get; set; }
        public In<float> in_imag { private get; set; }
        public In<bool> data_valid { private get; set; }
        public In<bool> data_ack { private get; set; }
        public Out<float> out_real { private get; set; }
        public Out<float> out_imag { private get; set; }
        public Out<bool> data_req { private get; set; }
        public Out<bool> data_ready { private get; set; }
        public In<bool> data_fin_in { private get; set; } // CK
        public Out<bool> data_fin_out { private get; set; } // CK
        public In<StdLogic> CLK { private get; set; }

        public FFT()
        {
        }

        private async void Process()
        {
            float[,] sample = new float[16, 2];
            uint index;

            data_fin_out.Next = false;
            do
            {
                data_req.Next = false;
                data_ready.Next = false;
                index = 0;
                //Reading in the Samples
                Log.WriteLine();
                Log.WriteLine("Reading in the samples...");
                while (index < 16)
                {
                    data_req.Next = true;
                    do { await RisingEdge(CLK); }
                    while (!data_valid.Cur && !data_fin_in.Cur);
                    if (data_fin_in.Cur)
                        break;
                    sample[index, 0] = in_real.Cur;
                    sample[index, 1] = in_imag.Cur;
                    index++;
                    data_req.Next = false;
                    await RisingEdge(CLK);
                }

                if (index < 16)
                    break;

                index = 0;

                //////////////////////////////////////////////////////////////////////////
                ///  Computation - 1D Complex DFT In-Place DIF Computation Algorithm  ////
                //////////////////////////////////////////////////////////////////////////

                //Size of FFT, N = 2**M    
                uint N, M, len;
                float theta;
                float[,] W = new float[7, 2];
                float w_real, w_imag, w_rec_real, w_rec_imag, w_temp;

                //Initialize
                M = 4; N = 16;
                len = N / 2;
                theta = (float)(8.0 * Math.Atan(1.0) / N);

                Log.WriteLine();
                Log.WriteLine("Computing...");

                //Calculate the W-values recursively
                w_real = (float)Math.Cos(theta);
                w_imag = (float)-Math.Sin(theta);

                w_rec_real = 1;
                w_rec_imag = 0;

                index = 0;
                while (index < len - 1)
                {
                    w_temp = w_rec_real * w_real - w_rec_imag * w_imag;
                    w_rec_imag = w_rec_real * w_imag + w_rec_imag * w_real;
                    w_rec_real = w_temp;
                    W[index, 0] = w_rec_real;
                    W[index, 1] = w_rec_imag;
                    index++;
                }

                float tmp_real, tmp_imag, tmp_real2, tmp_imag2;
                uint stage, i, j, index2, windex, incr;

                //Begin Computation 
                stage = 0;

                len = N;
                incr = 1;

                while (stage < M)
                {
                    len = len / 2;

                    //First Iteration :  With No Multiplies
                    i = 0;

                    while (i < N)
                    {
                        index = i; index2 = index + len;

                        tmp_real = sample[index, 0] + sample[index2, 0];
                        tmp_imag = sample[index, 1] + sample[index2, 1];

                        sample[index2, 0] = sample[index, 0] - sample[index2, 0];
                        sample[index2, 1] = sample[index, 1] - sample[index2, 1];

                        sample[index, 0] = tmp_real;
                        sample[index, 1] = tmp_imag;

                        i = i + 2 * len;
                    }

                    //Remaining Iterations: Use Stored W
                    j = 1; windex = incr - 1;
                    while (j < len) // This loop executes N/2 times at first stage, .. once at last stage.
                    {
                        i = j;
                        while (i < N)
                        {
                            index = i;
                            index2 = index + len;

                            tmp_real = sample[index, 0] + sample[index2, 0];
                            tmp_imag = sample[index, 1] + sample[index2, 1];
                            tmp_real2 = sample[index, 0] - sample[index2, 0];
                            tmp_imag2 = sample[index, 1] - sample[index2, 1];

                            sample[index2, 0] = tmp_real2 * W[windex, 0] - tmp_imag2 * W[windex, 1];
                            sample[index2, 1] = tmp_real2 * W[windex, 1] + tmp_imag2 * W[windex, 0];

                            sample[index, 0] = tmp_real;
                            sample[index, 1] = tmp_imag;

                            i = i + 2 * len;
                        }
                        windex = windex + incr;
                        j++;
                    }
                    stage++;
                    incr = 2 * incr;
                }

                //////////////////////////////////////////////////////////////////////////
                //Writing out the normalized transform values in bit reversed order
                Unsigned bits_i, bits_index;
                bits_index = Unsigned.FromULong(0, 4);
                bits_i = Unsigned.FromULong(0, 4);
                i = 0;

                Log.WriteLine("Writing the transform values...");
                while (i < 16)
                {
                    bits_i = Unsigned.FromULong(i, 4);
                    bits_index[3] = bits_i[0];
                    bits_index[2] = bits_i[1];
                    bits_index[1] = bits_i[2];
                    bits_index[0] = bits_i[3];
                    index = (uint)bits_index;
                    out_real.Next = sample[index, 0];
                    out_imag.Next = sample[index, 1];
                    data_ready.Next = true;
                    do { await RisingEdge(CLK); }
                    while (!data_ack.Cur);
                    data_ready.Next = false;
                    i++;
                    await RisingEdge(CLK);
                }
                index = 0;
                Log.WriteLine("Done...");
            } while (!data_fin_in.Cur);
            data_fin_out.Next = true;
            DesignContext.ExitProcess();
        }

        protected override void Initialize()
        {
            AddThread(Process);
        }
    }

    class Source : Component
    {
        public In<bool> data_req { private get; set; }
        public Out<float> out_real { private get; set; }
        public Out<float> out_imag { private get; set; }
        public Out<bool> data_valid { private get; set; }
        public Out<bool> data_fin { private get; set; } // CK
        public In<StdLogic> CLK { private get; set; }

        public string RealPath { get; set; }
        public string ImagPath { get; set; }

        StreamReader _tr_real;
        StreamReader _tr_imag;

        protected override void Initialize()
        {
            AddThread(Process);
        }

        private float ReadNextValue(StreamReader sr)
        {
            string line;
            float value;
            do
            {
                line = sr.ReadLine();
                if (line == null)
                {
                    Log.WriteLine("End of Input Stream: Simulation Stops");
                    _tr_real.Close();
                    _tr_imag.Close();
                    data_fin.Next = true;
                    DesignContext.ExitProcess();
                }
            } while (!float.TryParse(line, out value));
            return value;
        }

        private async void Process()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            _tr_real = new StreamReader(RealPath);
            _tr_imag = new StreamReader(ImagPath);

            float tmp_val;
            data_valid.Next = false;
            data_fin.Next = false;

            while (true)
            {
                do
                { await RisingEdge(CLK); }
                while (!data_req.Cur);
                tmp_val = ReadNextValue(_tr_real);
                out_real.Next = tmp_val;
                tmp_val = ReadNextValue(_tr_imag);
                out_imag.Next = tmp_val;
                data_valid.Next = true;
                do
                { await RisingEdge(CLK); }
                while (data_req.Cur);
                data_valid.Next = false;
                await RisingEdge(CLK);
            }
        }
    }

    class Sink : Component
    {
        public In<bool> data_ready { private get; set; }
        public Out<bool> data_ack { private get; set; }
        public In<float> in_real { private get; set; }
        public In<float> in_imag { private get; set; }
        public In<bool> data_fin { private get; set; }
        public In<StdLogic> CLK { private get; set; }

        public string RealPath { get; set; }
        public string ImagPath { get; set; }

        private TextWriter _tw_real;
        private TextWriter _tw_imag;

        private async void Process()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            do
            { await RisingEdge(CLK); }
            while (!data_ready.Cur && !data_fin.Cur);
            if (data_fin.Cur)
                DesignContext.Stop();
            _tw_real.Write(in_real.Cur);
            _tw_real.WriteLine();
            _tw_imag.Write(in_imag.Cur);
            _tw_imag.WriteLine();
            data_ack.Next = true;
            do
            { await RisingEdge(CLK); }
            while (data_ready.Cur);
            data_ack.Next = false;
        }

        protected override void Initialize()
        {
            _tw_real = new StreamWriter(RealPath);
            _tw_imag = new StreamWriter(ImagPath);

            AddThread(Process);
        }

        protected override void OnSimulationStopped()
        {
            _tw_real.Close();
            _tw_imag.Close();
        }
    }

    static class FFT_FP_Test
    {
        public static void GenTestData()
        {
            Console.WriteLine("Generating test data...");

            TextWriter tw_real = new StreamWriter("in_real.large");
            TextWriter tw_imag = new StreamWriter("in_imag.large");
            Random rnd = new Random();
            for (int i = 0; i < 10000; i++)
            {
                tw_real.WriteLine(rnd.NextDouble() + " ");
                tw_imag.WriteLine(rnd.NextDouble() + " ");
            }
            tw_real.Close();
            tw_imag.Close();

            Console.WriteLine("done");
        }

        public static void Run()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            GenTestData();

            Signal<float> in_real = new Signal<float>();
            Signal<float> in_imag = new Signal<float>();
            Signal<bool> data_valid = new Signal<bool>();
            Signal<bool> data_ack = new Signal<bool>();
            Signal<float> out_real = new Signal<float>();
            Signal<float> out_imag = new Signal<float>();
            Signal<bool> data_req = new Signal<bool>();
            Signal<bool> data_ready = new Signal<bool>();
            Signal<bool> data_fin1 = new Signal<bool>();
            Signal<bool> data_fin2 = new Signal<bool>();
            SLSignal clk = new SLSignal();

            Clock cgen = new Clock(new Time(10.0, ETimeUnit.ns), 0.5)
            {
                Clk = clk
            };
            FFT fft = new FFT()
            {
                CLK = clk,
                in_real = in_real,
                in_imag = in_imag,
                data_valid = data_valid,
                data_ack = data_ack,
                out_real = out_real,
                out_imag = out_imag,
                data_req = data_req,
                data_ready = data_ready,
                data_fin_in = data_fin1,
                data_fin_out = data_fin2
            };
            Source source = new Source()
            {
                RealPath = "in_real.large",
                ImagPath = "in_imag.large",

                data_req = data_req,
                out_real = in_real,
                out_imag = in_imag,
                data_valid = data_valid,
                data_fin = data_fin1,
                CLK = clk
            };
            Sink sink = new Sink()
            {
                RealPath = "out_real.large",
                ImagPath = "out_imag.large",

                data_ready = data_ready,
                data_ack = data_ack,
                in_real = out_real,
                in_imag = out_imag,
                data_fin = data_fin2,
                CLK = clk
            };

            DesignContext.Instance.Elaborate();
            Log.Verbose = true;
            long startTicks = DateTime.Now.Ticks;
            DesignContext.Instance.Simulate(long.MaxValue);
            long endTicks = DateTime.Now.Ticks;
            double durationMS = (double)(endTicks - startTicks) / 10000.0;
            Console.WriteLine("Analysis took " + durationMS + "ms");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            FFT_FP_Test.Run();
        }
    }
}
