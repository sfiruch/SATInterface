using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class DarkerPerformanceTest
    {
        [TestMethod]
        public void DarkerPerformance()
        {
            using var m = new Model(); m.Configuration.Verbosity = 0;

            m.Configuration.Solver = InternalSolver.CryptoMiniSat;

            var I0 = m.AddUIntVar(255);
            var I1 = m.AddUIntVar(255);
            var I2 = m.AddUIntVar(255);
            var I3 = m.AddUIntVar(255);
            var I4 = m.AddUIntVar(255);
            var I5 = m.AddUIntVar(255);
            var I6 = m.AddUIntVar(255);
            var I7 = m.AddUIntVar(255);

            var H = new UIntVar[8];
            H[0] = m.AddUIntVar(0x7A);
            H[1] = m.AddUIntVar(0x7A);
            H[2] = m.AddUIntVar(0x7A);
            H[3] = m.AddUIntVar(40);
            H[4] = m.AddUIntVar(0x7A);
            H[5] = m.AddUIntVar(0x7A);
            H[6] = m.AddUIntVar(0x7A);
            H[7] = m.AddUIntVar(0x7A);

            //H0 >= 21, H0 <= 7A
            m.AddConstr(0x21 <= H[0]);
            m.AddConstr(0x21 <= H[1]);
            m.AddConstr(0x21 <= H[2]);
            m.AddConstr(0x21 <= H[3]);
            m.AddConstr(0x21 <= H[4]);
            m.AddConstr(0x21 <= H[5]);
            m.AddConstr(0x21 <= H[6]);
            m.AddConstr(0x21 <= H[7]);

            //I0 + I1 = I2
            m.AddConstr(I0 + I1 == I2);
            m.AddConstr(I2 - 10 == I3);
            m.AddConstr(I4 + I5 == I6);
            m.AddConstr(I6 - 10 == I7);

            m.AddConstr(H[0] == I2);
            m.AddConstr(H[1] == I4);
            m.AddConstr(H[0] == H[1]);
            m.AddConstr(H[2] == H[1] - 1);
            m.AddConstr(H[3] <= 40);
            m.AddConstr(H[4] == 'a');
            m.AddConstr(H[5] == 'h');
            m.AddConstr(H[6] == 'o');
            m.AddConstr(H[7] == 'j');

            var numFound = 0;
            m.EnumerateSolutions(H, () =>
            {
                numFound++;
                if (numFound > 1000)
                    m.Abort();
            });

            Assert.AreEqual(numFound, 712);
        }
    }
}
