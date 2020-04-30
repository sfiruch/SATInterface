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
                    using var m = new Model();
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
                    using var m = new Model();
                    var v = m.AddVars(i);
                    m.AddConstr(m.Sum(v) >= j);

                    m.Minimize(m.Sum(v));

                    Assert.AreEqual(i >= j ? State.Satisfiable : State.Unsatisfiable, m.State);

                    if (i >= j)
                        Assert.AreEqual(j, v.Count(r => r.X));
                }
        }

        [TestMethod]
        public void LETest()
        {
            for (var j = 0; j < 20; j++)
                for (var i = 0; i < 20; i++)
                {
                    using var m = new Model();
                    var v = m.AddVars(i);
                    m.AddConstr(m.Sum(v) <= j);

                    m.Maximize(m.Sum(v));

                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(Math.Min(i, j), v.Count(r => r.X));
                }
        }
    }
}
