using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        void Pigeonhole(int _holes, int _pigeons, Model.AtMostOneOfMethod _method)
        {
            using var m = new Model();
            var assignment = m.AddVars(_holes, _pigeons);

            for (var h = 0; h < _holes; h++)
                m.AddConstr(m.AtMostOneOf(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]), _method));

            for (var p = 0; p < _pigeons; p++)
                m.AddConstr(
                    m.Or(Enumerable.Range(0, _holes).Select(h => assignment[h, p]))
                    & m.AtMostOneOf(Enumerable.Range(0, _holes).Select(h => assignment[h, p]), _method));

            m.Solve();

            Assert.AreEqual(_holes >= _pigeons ? State.Satisfiable : State.Unsatisfiable, m.State, $"{_holes} {_pigeons}");
        }

        [TestMethod]
        public void PigeonholeRandomCommander()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.AtMostOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeRandomSequential()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.AtMostOneOfMethod.Sequential);
        }

        [TestMethod]
        public void PigeonholeRandomPairwise()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.AtMostOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeRandomOneHot()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.AtMostOneOfMethod.OneHot);
        }

        [TestMethod]
        public void PigeonholeSingleCommander()
        {
            Pigeonhole(100, 1, Model.AtMostOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSingleSequential()
        {
            Pigeonhole(100, 1, Model.AtMostOneOfMethod.Sequential);
        }

        [TestMethod]
        public void PigeonholeSinglePairwise()
        {
            Pigeonhole(100, 1, Model.AtMostOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeSingleOneHot()
        {
            Pigeonhole(100, 1, Model.AtMostOneOfMethod.OneHot);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATCommander()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.AtMostOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATOneHot()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.AtMostOneOfMethod.OneHot);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATPairwise()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.AtMostOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATSequential()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.AtMostOneOfMethod.Sequential);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATCommander()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size-1, size, Model.AtMostOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATSequential()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size - 1, size, Model.AtMostOneOfMethod.Sequential);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATOneHot()
        {
            for (var size = 1; size < 11; size++)
                Pigeonhole(size - 1, size, Model.AtMostOneOfMethod.OneHot);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATPairwise()
        {
            for (var size = 1; size < 11; size++)
                Pigeonhole(size-1, size, Model.AtMostOneOfMethod.Pairwise);
        }
    }
}
