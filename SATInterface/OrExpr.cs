using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class OrExpr:BoolExpr
    {
        internal readonly BoolExpr[] elements;

        private OrExpr(BoolExpr[] _elems)
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
                else if (ReferenceEquals(es, TRUE))
                    return TRUE;
                else if (es is OrExpr)
                    foreach (var subE in ((OrExpr)es).elements)
                        res.Add(subE);
                else if (!ReferenceEquals(es, FALSE))
                    res.Add(es);

            if (!res.Any())
                return FALSE;
            else if(res.Count==1)
                return res.Single();
            else
            {
                foreach (var subE in res.OfType<NotExpr>())
                    if (res.Contains(subE.inner))
                        return TRUE;

                var andExpr = (AndExpr)res.FirstOrDefault(e => e is AndExpr);
                if (!ReferenceEquals(andExpr, null))
                {
                    var otherElems = res.Where(e => !ReferenceEquals(e, andExpr));
                    return AndExpr.Create(andExpr.elements.Select(e => OrExpr.Create(otherElems.Concat(new BoolExpr[] { e }))));
                }

                return new OrExpr(res.ToArray());
            }
        }

        public override string ToString() => "(" + string.Join(" | ", elements.Select(e => e.ToString()).ToArray()) + ")";

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        public override bool X
        {
            get
            {
                return elements.Any(e => e.X);
            }
        }

        public override bool Equals(object _obj)
        {
            var other = _obj as OrExpr;
            if (ReferenceEquals(other, null))
                return false;

            return elements.Union(other.elements).Count() == elements.Length;
        }

        private int hashCode;
        public override int GetHashCode()
        {
            if (hashCode == 0)
            {
                hashCode = elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b) ^ (1 << 29);
                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
