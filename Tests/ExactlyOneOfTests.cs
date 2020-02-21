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
            using var m = new Model();
            var assignment = m.AddVars(_holes, _pigeons);

            for (var h = 0; h < _holes; h++)
                m.AddConstr(
                    !m.Or(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]))
                    | m.ExactlyOneOf(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]), _method));

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
                Pigeonhole(RNG.Next(0, 11), RNG.Next(0, 11), Model.ExactlyOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeRandomBinary()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.BinaryCount);
        }

        [TestMethod]
        public void PigeonholeRandomOneHot()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 11), RNG.Next(0, 11), Model.ExactlyOneOfMethod.OneHot);
        }

        [TestMethod]
        public void PigeonholeRandomUnary()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeRandomSequential()
        {
            var RNG = new Random(1);
            for (var i = 0; i < 50; i++)
                Pigeonhole(RNG.Next(0, 12), RNG.Next(0, 12), Model.ExactlyOneOfMethod.Sequential);
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
        public void PigeonholeSingleOneHot()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.OneHot);
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
        public void PigeonholeSingleSequential()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.Sequential);
        }

        [TestMethod]
        public void PigeonholeSingleTwoFactor()
        {
            Pigeonhole(100, 1, Model.ExactlyOneOfMethod.TwoFactor);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATCommander()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.Commander);
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
        public void PigeonholeSymmetricSATOneHot()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.OneHot);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATUnary()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeSymmetricSATSequential()
        {
            for (var size = 1; size < 20; size++)
                Pigeonhole(size, size, Model.ExactlyOneOfMethod.Sequential);
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
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.Commander);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATSequential()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.Sequential);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATPairwise()
        {
            for (var size = 1; size < 10; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.Pairwise);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATOneHot()
        {
            for (var size = 1; size < 10; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.OneHot);
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
            for (var size = 1; size < 11; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.UnaryCount);
        }

        [TestMethod]
        public void PigeonholeSymmetricUNSATTwoFactor()
        {
            for (var size = 1; size < 12; size++)
                Pigeonhole(size - 1, size, Model.ExactlyOneOfMethod.TwoFactor);
        }

        [TestMethod]
        public void CornerTilePacking()
        {
            const int C = 2;
            const int N = C * C * C * C;

            const int W = C * C;
            const int H = C * C;

            using var m = new Model();
            var vXYC = m.AddVars(W, H, C);
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, C).Select(c => vXYC[x, y, c])));

            for (var n = 0; n < N; n++)
            {
                var c1 = n % C;
                var c2 = (n / C) % C;
                var c3 = (n / C / C) % C;
                var c4 = (n / C / C / C) % C;
                var l = new List<BoolExpr>();
                for (var y = 0; y < H; y++)
                    for (var x = 0; x < W; x++)
                        l.Add((vXYC[x, y, c1] & vXYC[(x + 1) % W, y, c2] & vXYC[x, (y + 1) % H, c3] & vXYC[(x + 1) % W, (y + 1) % H, c4]));

                m.AddConstr(m.ExactlyOneOf(l, Model.ExactlyOneOfMethod.BinaryCount));
            }

            m.Solve();

            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void SimpleBinary()
        {
            using var m = new Model();
            var v = m.AddVars(4);
            m.AddConstr(m.ExactlyOneOf(v, Model.ExactlyOneOfMethod.BinaryCount));

            m.AddConstr(!v[1]);
            m.AddConstr(!v[2]);
            m.AddConstr(!v[3]);
            m.Solve();

            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EnumerateBinary()
        {
            const int N = 4;

            using var m = new Model();
            var v = m.AddVars(N);
            m.AddConstr(m.ExactlyOneOf(v,Model.ExactlyOneOfMethod.BinaryCount));

            for (var i = 0; i < N; i++)
            {
                m.Solve();

                Assert.IsTrue(m.IsSatisfiable);

                var idx = Enumerable.Range(0, N).Single(j => v[j].X);

                Console.WriteLine($"not {idx}");
                m.AddConstr(!v[idx]);
            }

            m.Solve();
            Assert.IsTrue(m.IsUnsatisfiable);
        }
    }
}
