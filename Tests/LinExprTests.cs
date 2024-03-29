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
        public void NegateModel()
        {
            using var m = new Model();
            var le = new LinExpr();

            var v = m.AddVar();
            le.AddTerm(v, 1);

            for (var i = 0; i <= 1; i++)
            {
                m.AddConstr(le == i);
                m.AddConstr(le <= i);
                m.AddConstr(le >= i);
                m.AddConstr(le < i);
                m.AddConstr(le > i);
            }

            le = -le;
            for (var i = 0; i <= 1; i++)
            {
                m.AddConstr(le == i);
                m.AddConstr(le <= i);
                m.AddConstr(le >= i);
                m.AddConstr(le < i);
                m.AddConstr(le > i);
            }
        }

        [TestMethod]
        public void ToUIntPerformance()
        {
            using var m = new Model();
            var rng = new Random(0);
            var le = new LinExpr();
            for (var i = 0; i < 5000; i++)
            {
                var w = rng.Next(10) + 1;
                for (var bit = 0; bit < 8; bit++)
                    le.AddTerm(m.AddVar(), w * (1 << bit));
            }
            var x = (le == 5000);
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
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(m.Sum(v) >= 1);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 || m.ClauseCount == orFlattened + 1);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest2()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(-m.Sum(v) <= -1);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 + 1 || m.ClauseCount == orFlattened + 1);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest3()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(m.Sum(v) != 0);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 + 1 || m.ClauseCount == orFlattened + 1);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest4()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) != 0);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 + 1 || m.ClauseCount == orFlattened + 1);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest5()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) >= 1);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 + 1 || m.ClauseCount == orFlattened + 1);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest6()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(m.Sum(v) * 2 >= 2);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 || m.ClauseCount == orFlattened + 1);
        }

        [TestMethod]
        public void EfficientSetCoverEncodingTest7()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(m.Configuration.MaxClauseSize - 1);
            Assert.AreEqual(0, m.ClauseCount);

            m.Or(v).Flatten();
            var orFlattened = m.ClauseCount;

            m.AddConstr(m.Sum(v) * 3 >= 2);
            Assert.IsTrue(m.ClauseCount == orFlattened * 2 || m.ClauseCount == orFlattened + 1);
        }


        [TestMethod]
        public void EfficientSetPackingEncodingTest1()
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

                m.AddConstr(m.AtMostOneOf(v));
                amoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.Sum(v) <= 1);
                amoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(amoCC1, amoCC2);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest2()
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

                m.AddConstr(m.AtMostOneOf(v));
                amoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(-m.Sum(v) >= -1);
                amoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(amoCC1, amoCC2);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest3()
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

                m.AddConstr(m.AtMostOneOf(v));
                amoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) <= 1);
                amoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(amoCC1, amoCC2);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest4()
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

                m.AddConstr(m.AtMostOneOf(v));
                amoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(-m.Sum(v.Select((v, i) => i % 3 == 0 ? v : !v)) >= -1);
                amoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(amoCC1, amoCC2);
        }

        [TestMethod]
        public void EfficientSetPackingEncodingTest5()
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

                m.AddConstr(m.AtMostOneOf(v));
                amoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.Sum(v) * 2 <= 2);
                amoCC2 = m.ClauseCount;
            }

            Assert.AreEqual(amoCC1, amoCC2);
        }

        [TestMethod]
        public void EfficientSetPartitioningEncodingTest1()
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

                m.AddConstr(m.ExactlyOneOf(v));
                eoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.Sum(v) == 1);
                eoCC2 = m.ClauseCount;
            }
            Assert.AreEqual(eoCC1, eoCC2);
        }

        [TestMethod]
        public void EfficientSetPartitioningEncodingTest2()
        {
            using var m1 = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v1 = m1.AddVars(100);
            m1.AddConstr(m1.ExactlyOneOf(v1).Flatten());

            using var m2 = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v2 = m2.AddVars(100);
            m2.AddConstr((m2.Sum(v2.Select((v, i) => i % 3 == 0 ? v : !v)) == 1).Flatten());

            Assert.AreEqual(m1.ClauseCount, m2.ClauseCount);
        }

        [TestMethod]
        public void EfficientSetPartitioningEncodingTest3()
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

                m.AddConstr(m.ExactlyOneOf(v));
                eoCC1 = m.ClauseCount;
            }

            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
                var v = m.AddVars(100);
                Assert.AreEqual(0, m.ClauseCount);

                m.AddConstr(m.Sum(v) * 2 == 2);
                eoCC2 = m.ClauseCount;
            }
            Assert.AreEqual(eoCC1, eoCC2);
        }
    }
}
