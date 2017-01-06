using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class AndExpr:BoolExpr
    {
        internal readonly BoolExpr[] elements;

        private AndExpr(BoolExpr[] _elems)
        {
            elements = _elems;
        }

        public static BoolExpr Create(params BoolExpr[] _elems) => Create(_elems.AsEnumerable());

        public static BoolExpr Create(IEnumerable<BoolExpr> _elems)
        {
            var res = new HashSet<BoolExpr>();
            foreach (var es in _elems)
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, FALSE))
                    return FALSE;
                else if (es is AndExpr)
                    foreach (var subElement in ((AndExpr)es).elements)
                        res.Add(subElement);
                else if (!ReferenceEquals(es, TRUE))
                        res.Add(es);

            if (!res.Any())
                return TRUE;
            else if (res.Count==1)
                return res.Single();
            else
            {
                foreach (var subE in res.OfType<NotExpr>())
                    if (res.Contains(subE.inner))
                        return FALSE;

                return new AndExpr(res.ToArray());
            }
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        public override string ToString() => "(" + string.Join(" & ", elements.Select(e => e.ToString()).ToArray()) + ")";

        public override bool X
        {
            get
            {
                return elements.All(e => e.X);
            }
        }

        public override bool Equals(object _obj)
        {
            var other = _obj as AndExpr;
            if (ReferenceEquals(other, null))
                return false;

            return elements.Union(other.elements).Count() == elements.Length;
        }

        private int hashCode;
        public override int GetHashCode()
        {
            if (hashCode == 0)
            {
                hashCode = elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b) ^ (1 << 30);
                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
