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
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });
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

                Assert.AreEqual(State.Satisfiable, m.State);
                Assert.AreEqual(expected, res1.X);
                Assert.AreEqual(expected, res2.X);
            }
        }

        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataTestMethod]
        public void OrFlatten(int _vars)
        {
            Test(_vars, (m, vars) => m.Or(vars), vals => vals.Any(x => x));
        }

        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataTestMethod]
        public void AndFlatten(int _vars)
        {
            Test(_vars, (m, vars) => m.And(vars), vals => vals.All(x => x));
        }
    }
}
