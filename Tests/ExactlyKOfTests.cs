﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class ExactlyKOfTests
    {
        [DataRow(null)]
        [DataRow(Model.KOfMethod.BinaryCount)]
        [DataRow(Model.KOfMethod.LinExpr)]
        [DataRow(Model.KOfMethod.Sequential)]
        [DataRow(Model.KOfMethod.SortPairwise)]
        [DataRow(Model.KOfMethod.SortTotalizer)]
        [DataTestMethod]
        public void Random(Model.KOfMethod? _method)
        {
            var RNG = new Random(2);

            var sat = 0;
            for (var i = 0; i < 20; i++)
            {
                using var m = new Model(new Configuration() { Verbosity = 0 });
                var v = m.AddVars(RNG.Next(100) + 2);

                var constrs = RNG.Next(15) + 25;
                for (var r = 0; r < constrs; r++)
                {
                    var hs = v.Where(vi => RNG.NextDouble() < 10d/v.Length).Select(vi => RNG.Next(2) == 1 ? vi : !vi)
                        .ToArray();
                    m.AddConstr(m.ExactlyKOf(hs, hs.Length / 2, _method));
                }

                m.Solve();

                if (m.State == State.Satisfiable)
                    sat++;
            }

            Assert.AreEqual(5, sat);
        }
    }
}
