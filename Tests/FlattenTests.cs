using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class FlattenTests
    {
        public void Test(int _vars, Func<IEnumerable<BoolExpr>,BoolExpr> _generator, Func<IEnumerable<bool>,bool> _expectedResult)
        {
            for (var i = 0; i < 1 << _vars; i++)
            {
                var m = new Model();
                m.LogOutput = false;

                var v = m.AddVars(_vars);
                var bv = new bool[_vars];
                for (var j = 0; j < _vars; j++)
                    if (((i >> j) & 1) != 0)
                    {
                        m.AddConstr(v[j]);
                        bv[j] = true;
                    }
                    else
                    {
                        m.AddConstr(!v[j]);
                        bv[j] = false;
                    }

                var res1 = _generator(v);
                var res2 = res1.Flatten();
                var expected = _expectedResult(bv);

                m.Solve();

                Assert.IsTrue(m.IsSatisfiable);
                Assert.AreEqual(expected, res1.X);
                Assert.AreEqual(expected, res2.X);
            }
        }

        [TestMethod]
        public void OrFlatten()
        {
            Test(2, vars => OrExpr.Create(vars), vals => vals.Any(x => x));
            Test(3, vars => OrExpr.Create(vars), vals => vals.Any(x => x));
            Test(4, vars => OrExpr.Create(vars), vals => vals.Any(x => x));
            Test(5, vars => OrExpr.Create(vars), vals => vals.Any(x => x));
        }

        [TestMethod]
        public void AndFlatten()
        {
            Test(2, vars => AndExpr.Create(vars), vals => vals.All(x => x));
            Test(3, vars => AndExpr.Create(vars), vals => vals.All(x => x));
            Test(4, vars => AndExpr.Create(vars), vals => vals.All(x => x));
            Test(5, vars => AndExpr.Create(vars), vals => vals.All(x => x));
        }
    }
}
