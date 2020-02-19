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
        public void Test(int _vars, Func<Model, IEnumerable<BoolExpr>,BoolExpr> _generator, Func<IEnumerable<bool>,bool> _expectedResult)
        {
            for (var i = 0; i < 1 << _vars; i++)
            {
                using var m = new Model();
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

                var res1 = _generator(m, v);
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
            Test(2, (m, vars) => m.Or(vars), vals => vals.Any(x => x));
            Test(3, (m, vars) => m.Or(vars), vals => vals.Any(x => x));
            Test(4, (m, vars) => m.Or(vars), vals => vals.Any(x => x));
            Test(5, (m, vars) => m.Or(vars), vals => vals.Any(x => x));
        }

        [TestMethod]
        public void AndFlatten()
        {
            Test(2, (m, vars) => m.And(vars), vals => vals.All(x => x));
            Test(3, (m, vars) => m.And(vars), vals => vals.All(x => x));
            Test(4, (m, vars) => m.And(vars), vals => vals.All(x => x));
            Test(5, (m, vars) => m.And(vars), vals => vals.All(x => x));
        }
    }
}
