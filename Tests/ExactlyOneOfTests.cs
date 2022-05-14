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
        void Pigeonhole(int _holes, int _pigeons, Model.ExactlyOneOfMethod? _method)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var assignment = m.AddVars(_holes, _pigeons);

            for (var h = 0; h < _holes; h++)
                m.AddConstr(
                    !m.Or(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]))
                    | m.ExactlyOneOf(Enumerable.Range(0, _pigeons).Select(p => assignment[h, p]), _method));

            for (var p = 0; p < _pigeons; p++)
                m.AddConstr(
                    m.ExactlyOneOf(Enumerable.Range(0, _holes).Select(h => assignment[h, p]), _method));

            m.Solve();

            Assert.AreEqual(_holes >= _pigeons ? State.Satisfiable : State.Unsatisfiable, m.State, $"{_holes} {_pigeons}");
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void PigeonholeRandom(Model.ExactlyOneOfMethod? _method)
        {
            var RNG = new Random(1);
            for (var i = 0; i < 30; i++)
                Pigeonhole(RNG.Next(0, 11), RNG.Next(0, 11), _method);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void PigeonholeSingle(Model.ExactlyOneOfMethod? _method)
        {
            Pigeonhole(100, 1, _method);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void PigeonholeSymmetricSAT(Model.ExactlyOneOfMethod? _method)
        {
            for (var size = 1; size < 18; size++)
                Pigeonhole(size, size, _method);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void PigeonholeSymmetricUNSAT(Model.ExactlyOneOfMethod? _method)
        {
            for (var size = 1; size < 9; size++)
                Pigeonhole(size - 1, size, _method);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        //[DataRow(Model.ExactlyOneOfMethod.OneHot)]
        //[DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void PigeonholeSymmetricUNSATDifficult(Model.ExactlyOneOfMethod? _method)
        {
            Pigeonhole(11, 12, _method);
        }

        [TestMethod]
        public void CornerTilePacking()
        {
            const int C = 2;
            const int N = C * C * C * C;

            const int W = C * C;
            const int H = C * C;

            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
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

            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void SimpleBinary(Model.ExactlyOneOfMethod? _method)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(4);
            m.AddConstr(m.ExactlyOneOf(v, _method));

            m.AddConstr(!v[1]);
            m.AddConstr(!v[2]);
            m.AddConstr(!v[3]);
            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void EnumerateBinary(Model.ExactlyOneOfMethod? _method)
        {
            const int N = 4;

            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(N);
            m.AddConstr(m.ExactlyOneOf(v,_method));

            for (var i = 0; i < N; i++)
            {
                m.Solve();

                Assert.AreEqual(State.Satisfiable, m.State);

                var idx = Enumerable.Range(0, N).Single(j => v[j].X);

                Console.WriteLine($"not {idx}");
                m.AddConstr(!v[idx]);
            }

            m.Solve();
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void MinimizeToTwo(Model.ExactlyOneOfMethod? _method)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(7);
            m.AddConstr(m.Or(v));
            m.AddConstr(!m.ExactlyOneOf(v, _method));

            m.Minimize(m.Sum(v));

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(2, v.Count(v => v.X));
        }


        [DataRow(null)]
        [DataRow(Model.ExactlyOneOfMethod.BinaryCount)]
        [DataRow(Model.ExactlyOneOfMethod.Commander)]
        [DataRow(Model.ExactlyOneOfMethod.OneHot)]
        [DataRow(Model.ExactlyOneOfMethod.Pairwise)]
        [DataRow(Model.ExactlyOneOfMethod.PairwiseTree)]
        [DataRow(Model.ExactlyOneOfMethod.Sequential)]
        [DataRow(Model.ExactlyOneOfMethod.SequentialUnary)]
        [DataRow(Model.ExactlyOneOfMethod.TwoFactor)]
        [DataRow(Model.ExactlyOneOfMethod.UnaryCount)]
        [DataTestMethod]
        public void EfficientNotEncoding(Model.ExactlyOneOfMethod? _method)
        {
            int eoCC1;
            int eoCC2;

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.ExactlyOneOf(v, _method));
                eoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.ExactlyOneOf(v.Select((v, i) => i % 3 == 0 ? v : !v),_method));
                eoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(eoCC1, eoCC2);
        }
    }
}
