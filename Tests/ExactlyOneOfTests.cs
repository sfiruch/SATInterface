using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class ExactlyOneOfTests
    {
        void Pigeonhole(int _holes, int _pigeons, Model.ExactlyOneOfMethod _method)
        {
            var m = new Model();
            var assignment = m.AddVars(_holes, _pigeons);

            for (var h = 0; h < _holes; h++)
                m.AddConstr(
                    !OrExpr.Create(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]))
                    || m.ExactlyOneOf(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]), _method));

            for (var p = 0; p < _pigeons; p++)
                m.AddConstr(
                    m.ExactlyOneOf(Enumerable.Range(0, _holes).Select(h => assignment[h, p]), _method));

            m.Solve();

            Assert.AreEqual(_holes >= _pigeons, m.IsSatisfiable, $"{_holes} {_pigeons}");
        }

        [TestMethod]
        public void PigeonholeRandomCommander()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeRandomPairwise()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeRandomBinary()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.BinaryCount);
        }

        [TestMethod]
        public void PigeonholeRandomUnary()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeRandomTwoFactor()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.TwoFactor);
        }

        [TestMethod]
        public void PigeonholeSingleCommander()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSinglePairwise()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeSingleBinary()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.BinaryCount);
        }

        [TestMethod]
        public void PigeonholeSingleUnary()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeSingleTwoFactor()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.TwoFactor);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATCommander()
        {
            for(var size=1;size<20;size++)
            Pigeonhole(size,size, Model.ExactlyOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATPairwise()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATBinary()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.BinaryCount);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATUnary()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATTwoFactor()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.TwoFactor);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATCommander()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size-1, size, Model.ExactlyOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATPairwise()
        {
            for (var size = 1; size < 11; size++)
                Pigeonhole(size-1, size, Model.ExactlyOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATBinary()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.BinaryCount);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATUnary()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATTwoFactor()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.TwoFactor);
        }
    }
}
