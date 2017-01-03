using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATInterface
{
    public class AndExpr:BoolExpr
    {
        public BoolExpr[] elements;

        public AndExpr(params BoolExpr[] _elems) : this(_elems.AsEnumerable())
        {
        }

        public AndExpr(IEnumerable<BoolExpr> _elems)
        {
            if (_elems.Any(e => ReferenceEquals(e, null)))
                throw new ArgumentNullException();

            var res = new List<BoolExpr>();
            foreach (var e in _elems)
                if (e is AndExpr)
                    res.AddRange(((AndExpr)e).elements);
                else if (ReferenceEquals(e, FALSE))
                {
                    elements = new BoolExpr[] { FALSE };
                    return;
                }
                else if (!ReferenceEquals(e, TRUE))
                    res.Add(e);

            elements = res.ToArray();
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        public override string ToString() => "(" + string.Join(" & ", elements.AsEnumerable()) + ")";

        internal override BoolExpr Simplify()
        {
            if (!ReferenceEquals(Simplified,null))
                return Simplified;

            List<BoolExpr> elems = new List<BoolExpr>(elements.Length);
            foreach(var e in elements)
            {
                var edm = e.Simplify();
                if (edm is AndExpr)
                    elems.AddRange(((AndExpr)edm).elements);
                else
                    elems.Add(edm);
            }

            if (elems.Contains(FALSE))
                return Simplified=FALSE;

            if (elems.All(e => ReferenceEquals(e, TRUE)))
                return Simplified=TRUE;

            var uniqueElems = elems.Where(e => !ReferenceEquals(e, TRUE)).Distinct().ToArray();
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
                                return Simplified=FALSE;

                    Simplified = new AndExpr(uniqueElems);
                    Simplified.Simplified = Simplified;
                    return Simplified;
            }
        }

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

        public override int GetHashCode() => elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b) ^ (1<<30);
    }
}
