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
            var res = new List<BoolExpr>();
            foreach (var es in _elems)
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, Model.False))
                    return Model.False;
                else if (es is AndExpr)
                    res.AddRange(((AndExpr)es).elements);
                else if (!ReferenceEquals(es, Model.True))
                    res.Add(es);

            //remove duplicates
            res = res.Distinct().ToList();

            if (!res.Any())
                return Model.True;
            else if (res.Count == 1)
                return res.Single();
            else
            {
                /*foreach (var subE in res)
                    if (res is NotExpr && res.Contains(((NotExpr)subE).inner))
                        return FALSE;*/

                return new AndExpr(res.ToArray());
            }
        }

        private BoolVar? flattenCache;
        public override BoolExpr Flatten()
        {
            //TODO: track equality for CSE
            if (!(flattenCache is null))
                return flattenCache;

            var model = EnumVars().First().Model;
            flattenCache = new BoolVar(model);
            model.AddConstr(OrExpr.Create(elements.Select(e => !e).Append(flattenCache)));
            foreach (var e in elements)
                model.AddConstr(e | !flattenCache);

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

        public override bool Equals(object _obj)
        {
            var other = _obj as AndExpr;
            if (ReferenceEquals(other, null))
                return false;

            if (elements.Length != other.elements.Length)
                return false;

            foreach(var a in elements)
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
                var hc = new HashCode();
                foreach (var e in elements)
                    hc.Add(e);

                hashCode = hc.ToHashCode();
                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
