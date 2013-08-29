/**
 * Copyright 2011 Christian Köllner
 * 
 * This file is part of System#.
 *
 * System# is free software: you can redistribute it and/or modify it under 
 * the terms of the GNU Lesser General Public License (LGPL) as published 
 * by the Free Software Foundation, either version 3 of the License, or (at 
 * your option) any later version.
 *
 * System# is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more 
 * details.
 *
 * You should have received a copy of the GNU General Public License along 
 * with System#. If not, see http://www.gnu.org/licenses/lgpl.html.
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.DataTypes;

namespace Test
{
    class TestFixPoint
    {
        /*
         * Code partially ported from:
         * 
         * Chapter 2, Programs 3-5, Fig. 2.8-2.10
         * Gerald/Wheatley, APPLIED NUMERICAL ANALYSIS (fourth edition)
         * Addison-Wesley, 1989
         */
        
        public static void ComputeLU(SFix[,] A, int iw, int fw)
        {
            int n = A.GetLength(0);

            SFix sum;
            SFix diag = (SFix.FromLong(1, iw, fw) / A[0, 0]).Resize(iw, fw);
            for (int i = 1; i < n; i++) 
                A[0, i] = (A[0, i] * diag).Resize(iw, fw);

            /* 
            *  Now complete the computing of L and U elements.
            *  The general plan is to compute a column of L's, then
            *  call pivot to interchange rows, and then compute
            *  a row of U's.
            */

            int nm1 = n - 1;
            for (int j = 1; j < nm1; j++)
            {
                /* column of L's */
                for (int i = j; i < n; i++)
                {
                    sum = SFix.FromLong(0, iw, fw);
                    for (int k = 0; k < j; k++) 
                        sum = (sum + A[i, k] * A[k, j]).Resize(iw, fw);
                    A[i, j] = (A[i, j] - sum).Resize(iw, fw);
                }
                /* row of U's */
                diag = (SFix.FromLong(1, iw, fw) / A[j, j]).Resize(iw, fw);
                for (int k = j + 1; k < n; k++)
                {
                    sum = SFix.FromLong(0, iw, fw);
                    for (int i = 0; i < j; i++) 
                        sum = (sum + A[j, i] * A[i, k]).Resize(iw, fw);
                    A[j, k] = ((A[j, k] - sum) * diag).Resize(iw, fw);
                }
            }

            /* still need to get last element in L Matrix */

            sum = SFix.FromLong(0, iw, fw);
            for (int k = 0; k < nm1; k++) 
                sum = (sum + A[nm1, k] * A[k, nm1]).Resize(iw, fw);
            A[nm1, nm1] = (A[nm1, nm1] - sum).Resize(iw, fw);
        }

        public static void SubstituteLU(SFix[,] LU, SFix[] b, SFix[] y, SFix[] x)
        {
            int n = LU.GetLength(0);


            for (int i = 0; i < n; i++)
            {
                x[i] = b[i];
            }

            /* do forward substitution, replacing x vector. */

            x[0] /= LU[0, 0];
            for (int i = 1; i < n; i++)
            {
                SFix sum = SFix.FromLong(0, 1);
                for (int j = 0; j < i; j++) sum += LU[i, j] * x[j];
                x[i] = (x[i] - sum) / LU[i, i];
            }

            /* now get the solution vector, x[n-1] is already done */

            for (int i = n - 2; i >= 0; i--)
            {
                SFix sum = SFix.FromLong(0, 1);
                for (int j = i + 1; j < n; j++) sum += LU[i, j] * x[j];
                x[i] -= sum;
            }
        }

        static void PrintMatrix(SFix[,] A)
        {
            int n = A.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                    Console.WriteLine();

                for (int j = 0; j < n; j++)
                {
                    if (j > 0)
                        Console.Write(" ");
                    Console.Write(A[i, j].ToString(10, 2, 2));
                }
            }
        }

        public static void RunTest()
        {
            FixedPointSettings.GlobalDefaultRadix = 10;
            FixedPointSettings.GlobalOverflowMode = EOverflowMode.Fail;

            SFix s1 = SFix.FromDouble(1.0, 2, 2);
            SFix s2 = SFix.FromDouble(-0.75, 2, 2);
            SFix s3 = s1 / s2;

            Random rnd = new Random();
            int n = 10;
            int iw = 14;
            int fw = 14;
            double eps = Math.Pow(2.0, -fw);
            SFix[,] A = new SFix[n, n];
            double[,] Ad = new double[n, n];
            SFix[] b = new SFix[n];
            double[] bd = new double[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double a = 2.0 * rnd.NextDouble() - 1.0;
                    Ad[i, j] = a;
                }
            }
            //Ad = new double[,] { { 4, -2, 1 }, { -3, -1, 4 }, { 1, -1, 3 } };
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double a = Ad[i, j];
                    SFix c = SFix.FromDouble(a, iw, fw);
                    string cs = c.ToString();
                    double ar = c.DoubleValue;
                    double d = a - ar;
                    double da = Math.Abs(d);
                    Debug.Assert(da < eps);
                    A[i, j] = c;
                    Ad[i, j] = a;
                }
                double bi = 2.0 * rnd.NextDouble() - 1.0;
                b[i] = SFix.FromDouble(bi, 3, 33);
                bd[i] = bi;
            }
            SFix[,] Ac = (SFix[,])A.Clone();
            Console.WriteLine("Original matrix:");
            PrintMatrix(A);
            Console.WriteLine();
            ComputeLU(A, iw, fw);
            Console.WriteLine("LU:");
            PrintMatrix(A);
            Console.WriteLine();

            SFix[] x = new SFix[n];
            SFix[] y = new SFix[n];
            SubstituteLU(A, b, y, x);

            Console.WriteLine("Precisions:");
            for (int i = 0; i < n; i++)
            {
                SFix bc = Ac[i, 0] * x[0];
                for (int j = 1; j < n; j++)
                {
                    bc += Ac[i, j] * x[j];
                }
                SFix d = bc - b[i];
                Console.Write("b[" + i + "] = " + b[i].ToString(10, 2, 2));
                Console.Write(" / " + bc.ToString(10, 2, 2));
                Console.WriteLine(" / d = " + d.ToString(10, 2, 2));
            }
        }
    }
}
