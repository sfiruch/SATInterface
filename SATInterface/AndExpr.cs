using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    internal class AndExpr : BoolExpr
    {
        internal readonly BoolExpr[] elements;

        private AndExpr(BoolExpr[] _elems)
        {
            elements = _elems;
        }

        internal static BoolExpr Create(params BoolExpr[] _elems) => Create(_elems.AsEnumerable());

        internal static BoolExpr Create(IEnumerable<BoolExpr> _elems)
        {
            var res = new HashSet<BoolExpr>();
            foreach (var es in _elems)
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, Model.False))
                    return Model.False;
                else if (es is AndExpr ae)
                    foreach (var e in ae.elements)
                        res.Add(e);
                else if (!ReferenceEquals(es, Model.True))
                    res.Add(es);

            //TODO find fast way to solve set cover approximately for CSE
            //if (res.Count == 0)
            //    return Model.True;
            //if (res.Count == 1)
            //    return res.Single();

            //for (var i = 0; i < res.Count; i++)
            //    for (var j = 0; j < res.Count; j++)
            //        if (i != j)
            //            for (var k = 0; k < res.Count; k++)
            //            {
            //                var ei = res.ElementAt(i);
            //                var ej = res.ElementAt(j);
            //                var ek = res.ElementAt(k);
            //                if (i != k && j != k && model.ExprCache.TryGetValue(new AndExpr(new[] { ei, ej, ek }), out var lu) && lu.VarCount <= 1)
            //                {
            //                    res.Remove(ei);
            //                    res.Remove(ej);
            //                    res.Remove(ek);
            //                    res.Add(lu);
            //                    Console.Write("3");
            //                }
            //            }
            //for (var i = 0; i < res.Count; i++)
            //    for (var j = 0; j < res.Count; j++)
            //        if (i != j)
            //        {
            //            var ei = res.ElementAt(i);
            //            var ej = res.ElementAt(j);
            //            if (model.ExprCache.TryGetValue(new AndExpr(new[] { ei, ej }), out var lu) && lu.VarCount <= 1)
            //            {
            //                res.Remove(ei);
            //                res.Remove(ej);
            //                res.Add(lu);
            //                Console.Write("2");
            //            }
            //        }

            if (res.Count == 0)
                return Model.True;
            if (res.Count == 1)
                return res.Single();
            if (res.OfType<NotExpr>().Any(ne => res.Contains(ne.inner)))
                return Model.False;

            var ret = new AndExpr(res.ToArray());
            var model = ret.EnumVars().First().Model;
            if (model.Configuration.CommonSubexpressionElimination)
            {
                if (model.ExprCache.TryGetValue(ret, out var lu))
                    return lu;
                model.ExprCache[ret] = ret;
            }
            return ret;
        }

        private BoolExpr? flattenCache;
        public override BoolExpr Flatten()
        {
            if (!(flattenCache is null))
                return flattenCache;

            var model = EnumVars().First().Model;

            if (model.Configuration.CommonSubexpressionElimination
                && model.ExprCache.TryGetValue(this, out var lu) && lu.VarCount <= 1)
                return flattenCache = lu;

            flattenCache = new BoolVar(model);
            model.AddConstr(OrExpr.Create(elements.Select(e => !e).Append(flattenCache)));
            foreach (var e in elements)
                model.AddConstr(e | !flattenCache);

            if (model.Configuration.CommonSubexpressionElimination)
            {
                model.ExprCache[this] = flattenCache;
                model.ExprCache[!this] = !flattenCache;
            }

            return flattenCache;
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        public override string ToString() => "(" + string.Join(" & ", elements.Select(e => e.ToString()).ToArray()) + ")";

        public override bool X => elements.All(e => e.X);

        public override int VarCount => elements.Length;

        public override bool Equals(object _obj)
        {
            var other = _obj as AndExpr;
            if (ReferenceEquals(other, null))
                return false;

            if (elements.Length != other.elements.Length)
                return false;

            foreach (var a in elements)
                if (!other.elements.Contains(a))
                    return false;
            foreach (var a in other.elements)
                if (!elements.Contains(a))
                    return false;
            return true;
        }

        private int hashCode;
        public override int GetHashCode()
        {
            if (hashCode == 0)
            {
                hashCode = GetType().GetHashCode();

                //deliberatly stupid implementation to produce
                //order-independent hashcodes
                foreach (var e in elements)
                    hashCode += HashCode.Combine(e);

                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
