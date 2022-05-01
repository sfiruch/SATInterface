using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class LinExprTests
    {
        [TestMethod]
        public void EqualTest()
        {
            for (var j = 0; j < 50; j++)
                for (var i = 0; i < 50; i++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0
                    });
                    var v = m.AddVars(i);
                    m.AddConstr(m.Sum(v) == j);

                    m.Solve();

                    Assert.AreEqual(i >= j ? State.Satisfiable : State.Unsatisfiable, m.State);

                    if (i >= j)
                        Assert.AreEqual(j, v.Count(r => r.X));
                }
        }

        [TestMethod]
        public void GETest()
        {
            for (var j = 0; j < 25; j++)
                for (var i = 0; i < 25; i++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0
                    });
                    var v = m.AddVars(i);
                    m.AddConstr(m.Sum(v) >= j);

                    m.Minimize(m.Sum(v));

                    Assert.AreEqual(i >= j ? State.Satisfiable : State.Unsatisfiable, m.State, $"i={i}, j={j}");

                    if (i >= j)
                        Assert.AreEqual(j, v.Count(r => r.X), $"i={i}, j={j}");
                }
        }

        [TestMethod]
        public void GETest22()
        {
            var i = 2;
            var j = 2;
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(i);
            m.AddConstr(m.Sum(v) >= j);

            m.Minimize(m.Sum(v));

            Assert.AreEqual(i >= j ? State.Satisfiable : State.Unsatisfiable, m.State, $"i={i}, j={j}");

            if (i >= j)
                Assert.AreEqual(j, v.Count(r => r.X), $"i={i}, j={j}");
        }


        [TestMethod]
        public void LETest()
        {
            for (var j = 0; j < 20; j++)
                for (var i = 0; i < 20; i++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0
                    });
                    var v = m.AddVars(i);
                    m.AddConstr(m.Sum(v) <= j);

                    m.Maximize(m.Sum(v));

                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(Math.Min(i, j), v.Count(r => r.X));
                }
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest1()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.Sum(v) >= 1);
            Assert.AreEqual(1, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest2()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(-m.Sum(v) <= -1);
            Assert.AreEqual(1, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest3()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.Sum(v) != 0);
            Assert.AreEqual(1, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest4()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) != 0);
            Assert.AreEqual(1, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest5()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) >= 1);
            Assert.AreEqual(1, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest1()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.AtMostOneOf(v));
            var amoCC = m.ClauseCount;

            m.AddConstr(m.Sum(v) <= 1);
            Assert.AreEqual(amoCC * 2, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest2()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.AtMostOneOf(v));
            var amoCC = m.ClauseCount;

            m.AddConstr(-m.Sum(v) >= -1);
            Assert.AreEqual(amoCC * 2, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest3()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.AtMostOneOf(v));
            var amoCC = m.ClauseCount;

            m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) <= 1);
            Assert.AreEqual(amoCC * 2, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest4()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.AtMostOneOf(v));
            var amoCC = m.ClauseCount;

            m.AddConstr(-m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) >= -1);
            Assert.AreEqual(amoCC * 2, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPartitioningEncodingTest1()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.ExactlyOneOf(v));
            var eoCC = m.ClauseCount;

            m.AddConstr(m.Sum(v) == 1);
            Assert.AreEqual(eoCC * 2, m.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPartitioningEncodingTest2()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(100);
            Assert.AreEqual(0, m.ClauseCount);

            m.AddConstr(m.ExactlyOneOf(v));
            var eoCC = m.ClauseCount;

            m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) == 1);
            Assert.AreEqual(eoCC * 2, m.ClauseCount);
        }
    }
}
