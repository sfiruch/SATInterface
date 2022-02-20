﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    internal class OrExpr : BoolExpr
    {
        internal readonly BoolExpr[] elements;

        private OrExpr(BoolExpr[] _elems)
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
                else if (ReferenceEquals(es, Model.True))
                    return Model.True;
                else if (es is OrExpr oe)
                    foreach (var e in oe.elements)
                        res.Add(e);
                else if (es is AndExpr ae)
                    res.Add(ae.Flatten());
                else if (!ReferenceEquals(es, Model.False))
                    res.Add(es);

            //if (!(largestAnd is null))
            //{
            //    var orExprs = new BoolExpr[largestAnd.elements.Length];
            //    for (var j = 0; j < orExprs.Length; j++)
            //        orExprs[j] = OrExpr.Create(res.Append(largestAnd.elements[j]));
            //    return AndExpr.Create(orExprs);
            //}

            //TODO find fast way to solve set cover approximately for CSE
            //if (res.Count == 0)
            //    return Model.False;
            //if (res.Count == 1)
            //    return res.Single();
            //var model = res.First().EnumVars().First().Model;
            //for (var i = 0; i < res.Count; i++)
            //    for (var j = i + 1; j < res.Count; j++)
            //        for (var k = j + 1; k < res.Count; k++)
            //        {
            //            var ei = res.ElementAt(i);
            //            var ej = res.ElementAt(j);
            //            var ek = res.ElementAt(k);
            //            if (model.ExprCache.TryGetValue(new OrExpr(new[] { ei, ej, ek }), out var lu) && lu.VarCount <= 1)
            //            {
            //                res.Remove(ei);
            //                res.Remove(ej);
            //                res.Remove(ek);
            //                res.Add(lu);
            //                Console.Write("3");
            //            }
            //        }
            //for (var i = 0; i < res.Count; i++)
            //    for (var j = i + 1; j < res.Count; j++)
            //    {
            //        var ei = res.ElementAt(i);
            //        var ej = res.ElementAt(j);
            //        if (model.ExprCache.TryGetValue(new OrExpr(new[] { ei, ej }), out var lu) && lu.VarCount <= 1)
            //        {
            //            res.Remove(ei);
            //            res.Remove(ej);
            //            res.Add(lu);
            //            Console.Write("2");
            //        }
            //    }

            if (res.Count == 0)
                return Model.False;
            if (res.Count == 1)
                return res.Single();
            if (res.OfType<NotExpr>().Any(ne => res.Contains(ne.inner)))
                return Model.True;

            return new OrExpr(res.ToArray());
        }

        public override string ToString() => "(" + string.Join(" | ", elements.Select(e => e.ToString()).ToArray()) + ")";

        private BoolExpr? flattenCache;
        public override BoolExpr Flatten()
        {
            if (!(flattenCache is null))
                return flattenCache;

            var model = EnumVars().First().Model;

            flattenCache = model.AddVar();
            model.AddConstr(OrExpr.Create(elements.Append(!flattenCache)));
            foreach (var e in elements)
                model.AddConstr(!e | flattenCache);

            return flattenCache;
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        public override bool X => elements.Any(e => e.X);

        public override int VarCount => elements.Length;

        public override bool Equals(object? _obj)
        {
            var other = _obj as OrExpr;
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
