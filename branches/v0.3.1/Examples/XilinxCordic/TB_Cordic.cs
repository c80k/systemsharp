using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Cordic
{


    class Tesbernch_Cordic : Component
    {
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);
        private SLSignal _clk = new SLSignal();

// Rotate_Aus_Und_Eingangssignale
        private SLVSignal _X_IN = new SLVSignal(StdLogicVector._1s(10));
        private SLVSignal _Y_IN = new SLVSignal(StdLogicVector._1s(10));
        private SLVSignal _PHASE_IN = new SLVSignal(StdLogicVector._1s(10));
        private SLVSignal _X_OUT = new SLVSignal(StdLogicVector._1s(10))
        {  
            InitialValue = "0000000000"
        };
        
        private SLVSignal _Y_OUT = new SLVSignal(StdLogicVector._1s(10))
        {
            InitialValue = "0000000000"
        };
        private SLVSignal _PHASE_OUT = new SLVSignal(StdLogicVector._1s(10))
        {
            InitialValue = "0000000000"
        };

// Translate_Aus_Und_Eingangssignale
        private SLVSignal _X_IN1 = new SLVSignal(StdLogicVector._1s(10));
        private SLVSignal _Y_IN1 = new SLVSignal(StdLogicVector._1s(10));
        private SLVSignal _PHASE_OUT1 = new SLVSignal(StdLogicVector._1s(10))
        {
            InitialValue = "0000000000"
        };
        private SLVSignal _X_OUT1 = new SLVSignal(StdLogicVector._1s(10))
        {
            InitialValue = "0000000000"
        };

// SinAndCos_Aus_Und_Eingangssignale

        private SLVSignal _PHASE_IN2 = new SLVSignal(StdLogicVector._1s(10));        
        private SLVSignal _Y_OUT2 = new SLVSignal(StdLogicVector._1s(10))
        {
            InitialValue = "0000000000"
        };
        private SLVSignal _X_OUT2 = new SLVSignal(StdLogicVector._1s(10))
        {
            InitialValue = "0000000000"
        };

 //SinhAndCosh_Aus_Und_Eingangssignale

 //       private SLVSignal _PHASE_IN3 = new SLVSignal(StdLogicVector._1s(10));
 //       private SLVSignal _Y_OUT3 = new SLVSignal(StdLogicVector._1s(10))
 //       {
 //           InitialValue = "0000000000"
 //       };
 //       private SLVSignal _X_OUT2 = new SLVSignal(StdLogicVector._1s(10))
 //       {
 //           InitialValue = "0000000000"
 //       };
              

        private SLSignal RFD = new SLSignal();
        private SLSignal RDY = new SLSignal();

        
        private Clock _m_clk;
        //private XilinxCordic _m_signal;
        private XilinxCordic _translate;
        private XilinxCordic _rot;
        private XilinxCordic _SinAndCos;

       
        public async void Process()
        {
           
            _X_IN.Next = "0010110101";
            _Y_IN.Next = "0001000000";
            _PHASE_IN.Next = "1100110111";

            SFix x = SFix.FromSigned(_X_IN.Cur.SignedValue, 10 - 2);
            double xd = x.DoubleValue;

            SFix y = SFix.FromSigned(_Y_IN.Cur.SignedValue, 10 - 2);
            double yd = y.DoubleValue;

            SFix w = SFix.FromSigned(_PHASE_IN.Cur.SignedValue, 10 - 3);
            double wd = w.DoubleValue;

            double X_OU1 = (Math.Cos(wd)) * xd - (Math.Sin(wd)) * yd;
            double Y_OU1 = (Math.Cos(wd)) * xd + (Math.Sin(wd)) * yd;

            double x_ou1 = xd * xd + yd * yd;
            double x_ou2 = Math.Sqrt(x_ou1);
            double w_ou = Math.Atan2(xd, yd);

            double x_ou3 = Math.Cos(wd);
            double y_ou3 = Math.Sin(wd);

            await Tick;

            Console.WriteLine(" Rotate: "+ " " +  "x_out = " + _X_OUT.Cur.ToString() + " " +  "y_out=" + _Y_OUT.Cur.ToString() );
            Console.WriteLine(" Rotate in double : x_ou = " + X_OU1 + " y_ou = " + Y_OU1);
            Console.WriteLine(" Translate: " + " " + "x_out1 = " + _X_OUT1.Cur.ToString() + " " + "phse_out 1 =" + _PHASE_OUT1.Cur.ToString());
            Console.WriteLine(" Translate in double : x_ou = " + x_ou2 + " Winkel " + w_ou);
            Console.WriteLine(" SinAndCos: "+ "  " + "x_out2 = " + _X_OUT2.Cur.ToString() + " " + "y_out2 =" + _Y_OUT2.Cur.ToString());
            Console.WriteLine("SinAndCos in double : x_ou = " + x_ou3 + "  + y_ou = " + y_ou3);
           // Console.WriteLine(" SinhAndCosh: " + "  " + "x_out2 = " + _X_OUT2.Cur.ToString() + " " + "y_out2 =" + _Y_OUT2.Cur.ToString());

            while (true)
            {
                await Tick;
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Process, _clk.RisingEdge, _clk);
        }

        public Tesbernch_Cordic()
        {
            _m_clk = new Clock(ClockPeriod)
            {
                Clk = _clk
            };

      // Instanziierung von Translate

            _translate = new XilinxCordic()
            {
                FunctionalSelection = XilinxCordic.EFunctionalSelection.Translate,
                Clk = _clk,
                X_In = _X_IN1,
                Y_In = _Y_IN1,               
                X_Out = _X_OUT1,                          
                Phase_Out = _PHASE_OUT1,
            };

       // Instanziierung von SinAndCos

            _SinAndCos = new XilinxCordic()
            {
                FunctionalSelection = XilinxCordic.EFunctionalSelection.SinAndCos,
                Clk = _clk,                
                Phase_In = _PHASE_IN2,           
                X_Out = _X_OUT2,
                Y_Out = _Y_OUT2,
                
            };

            
      // Instanziierung von Rotate

            _rot = new XilinxCordic()
            {
                FunctionalSelection = XilinxCordic.EFunctionalSelection.Rotate,

                Clk = _clk,
                X_In = _X_IN,
                Y_In = _Y_IN,
                Phase_In = _PHASE_IN,
                X_Out = _X_OUT,
                Y_Out = _Y_OUT,
                Phase_Out = _PHASE_OUT,
                //...
            };      
       

        }



        class Program
        {
            static void Main(string[] args)
            {
                Tesbernch_Cordic tb = new Tesbernch_Cordic();
                DesignContext.Instance.Elaborate();
                DesignContext.Instance.Simulate(100 * Tesbernch_Cordic.ClockPeriod);
                //DesignContext.Instance.Simulate(10 * (Tesbernch_Cordic.DataWidth + 3) * Tesbernch_Cordic.ClockPeriod);

                // Now convert the design to VHDL and embed it into a Xilinx ISE project
                XilinxProject project = new XilinxProject(@".\hdl_output", "XilinxCordic");
                project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
                project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
                project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
                project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
                project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

                VHDLGenerator codeGen = new VHDLGenerator();
                SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen);
                project.Save();
            }
        }
    }
}
