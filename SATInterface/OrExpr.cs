using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATInterface
{
    public class OrExpr:BoolExpr
    {
        public BoolExpr[] elements;

        public OrExpr(params BoolExpr[] _elems):this(_elems.AsEnumerable())
        {
        }

        public OrExpr(IEnumerable<BoolExpr> _elems)
        {
            if (_elems.Any(e => ReferenceEquals(e, null)))
                throw new ArgumentNullException();

            var res = new List<BoolExpr>();
            foreach (var e in _elems)
                if (e is OrExpr)
                    res.AddRange(((OrExpr)e).elements);
                else if (ReferenceEquals(e, TRUE))
                {
                    elements = new BoolExpr[] { TRUE };
                    return;
                }
                else if (!ReferenceEquals(e, FALSE))
                    res.Add(e);

            elements = res.ToArray();
        }

        public override string ToString() => "(" + string.Join(" | ",elements.AsEnumerable()) + ")";

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        internal override BoolExpr Simplify()
        {
            if (!ReferenceEquals(Simplified,null))
                return Simplified;

            List<BoolExpr> elems = new List<BoolExpr>();
            foreach (var e in elements)
            {
                var edm = e.Simplify();
                if (edm is OrExpr)
                    elems.AddRange(((OrExpr)edm).elements);
                else if(!ReferenceEquals(edm, FALSE))
                    elems.Add(edm);
            }

            if (elems.Contains(TRUE))
                return Simplified=TRUE;

            var uniqueElems = elems.Distinct().ToArray();
            switch (uniqueElems.Length)
            {
                case 0:
                    return Simplified=FALSE;
                case 1:
                    return Simplified=uniqueElems.Single();
                default:
                    for (var i = 0; i < uniqueElems.Length; i++)
                        if (uniqueElems[i] is NotExpr)
                            if (uniqueElems.Contains(((NotExpr)uniqueElems[i]).inner))
                                return Simplified=TRUE;

                    var andExpr = (AndExpr)uniqueElems.FirstOrDefault(e => e is AndExpr);
                    if (ReferenceEquals(andExpr, null))
                    {
                        Simplified = new OrExpr(uniqueElems);
                        Simplified.Simplified = Simplified;
                        return Simplified;
                    }

                    var otherElems = uniqueElems.Where(e => !ReferenceEquals(e,andExpr));
                    return Simplified = new AndExpr(andExpr.elements.Select(e => new OrExpr(otherElems.Concat(new BoolExpr[] { e })))).Simplify();
            }
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

        public override int GetHashCode() => elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b) ^ (1 << 29);
    }
}
