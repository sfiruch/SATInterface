using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class OrExpr : BoolExpr
    {
        internal readonly BoolExpr[] elements;

        private OrExpr(BoolExpr[] _elems)
        {
            elements = _elems;
        }

        public static BoolExpr Create(params BoolExpr[] _elems) => Create(_elems.AsEnumerable());

        public static BoolExpr Create(IEnumerable<BoolExpr> _elems)
        {
            var res = new List<BoolExpr>();
            foreach (var es in _elems)
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, True))
                    return True;
                else if (es is OrExpr)
                    res.AddRange(((OrExpr)es).elements);
                else if (!ReferenceEquals(es, False))
                    res.Add(es);

            //remove duplicates
            res = res.Distinct().ToList();
            /*for (var i = 0; i < res.Count; i++)
                for (var j = i + 1; j < res.Count; j++)
                    if (ReferenceEquals(res[i], res[j]))
                    {
                        res.RemoveAt(j);
                        j--;
                    }*/

            if (res.Count == 0)
                return False;
            else if (res.Count == 1)
                return res.Single();
            else
            {
                /*foreach (var subE in res)
                    if (subE is NotExpr && res.Contains(((NotExpr)subE).inner))
                        return TRUE;*/

                if (res.Any(e => e is AndExpr a && a.elements.Length >= 8))
                    for (var i = 0; i < res.Count; i++)
                        if (res[i] is AndExpr andExpr)
                        {
                            var orExprs = new BoolExpr[andExpr.elements.Length];
                            for (var j = 0; j < orExprs.Length; j++)
                            {
                                res[i] = andExpr.elements[j];
                                orExprs[j] = OrExpr.Create(res);
                            }

                            return AndExpr.Create(orExprs);
                        }

                for (var i = 0; i < res.Count; i++)
                    if (res[i] is AndExpr andExpr)
                        res[i] = andExpr.Flatten();

                //find v !a
                foreach (var e in res)
                    if (e is NotExpr && res.Contains(((NotExpr)e).inner))
                        return True;

                return new OrExpr(res.ToArray());
            }
        }

        public override string ToString() => "(" + string.Join(" | ", elements.Select(e => e.ToString()).ToArray()) + ")";

        public override BoolExpr Flatten()
        {
            var model = EnumVars().First().Model;
            var res = new BoolVar(model);
            model.AddConstr(OrExpr.Create(elements.Append(!res)));
            foreach (var e in elements)
                model.AddConstr(!e | res);

            //model.AddConstr(!res || this);

            return res;
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        public override bool X => elements.Any(e => e.X);

        public override bool Equals(object _obj)
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
                hashCode = (int)Model.RotateLeft((uint)elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b), 4) ^ (1 << 29);
                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
