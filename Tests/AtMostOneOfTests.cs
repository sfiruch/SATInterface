﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class AtMostOneOfTests
    {
        void Pigeonhole(int _holes, int _pigeons, Model.AtMostOneOfMethod? _method)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var assignment = m.AddVars(_holes, _pigeons);

            for (var h = 0; h < _holes; h++)
                m.AddConstr(m.AtMostOneOf(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]), _method));

            for (var p = 0; p < _pigeons; p++)
                m.AddConstr(
                    m.Or(Enumerable.Range(0, _holes).Select(h => assignment[h, p])).Flatten()
                    & m.AtMostOneOf(Enumerable.Range(0, _holes).Select(h => assignment[h, p]), _method));

            m.Solve();

            Assert.AreEqual(_holes >= _pigeons ? State.Satisfiable : State.Unsatisfiable, m.State, $"{_holes} {_pigeons}");
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        [DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void PigeonholeRandom(Model.AtMostOneOfMethod? _method)
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 11), RNG.Next(0, 11), _method);
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        [DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void PigeonholeSingle(Model.AtMostOneOfMethod? _method)
        {
            Pigeonhole(100, 1, _method);
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        [DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void PigeonholeSymmetricSAT(Model.AtMostOneOfMethod? _method)
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, _method);
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        [DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void PigeonholeSymmetricUNSAT(Model.AtMostOneOfMethod? _method)
        {
            for (var size = 1; size < 10; size++)
                Pigeonhole(size-1, size, _method);
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        //[DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void PigeonholeSymmetricUNSATDifficult(Model.AtMostOneOfMethod? _method)
        {
            Pigeonhole(11, 12, _method);
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        [DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void MinimizeToTwo(Model.AtMostOneOfMethod? _method)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(7);
            m.AddConstr(!m.AtMostOneOf(v, _method));

            m.Minimize(m.Sum(v));

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(2, v.Count(v => v.X));
        }

        [DataRow(null)]
        [DataRow(Model.AtMostOneOfMethod.BinaryCount)]
        [DataRow(Model.AtMostOneOfMethod.Commander)]
        [DataRow(Model.AtMostOneOfMethod.OneHot)]
        [DataRow(Model.AtMostOneOfMethod.Pairwise)]
        [DataRow(Model.AtMostOneOfMethod.PairwiseTree)]
        [DataRow(Model.AtMostOneOfMethod.Sequential)]
        [DataTestMethod]
        public void EfficientNotEncoding(Model.AtMostOneOfMethod? _method)
        {
            int amoCC1;
            int amoCC2;

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.AtMostOneOf(v, _method));
                amoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.AtMostOneOf(v.Select((v, i) => i % 3 == 0 ? v : !v), _method));
                amoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(amoCC1, amoCC2);
        }
    }
}
