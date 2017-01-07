using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class OrExpr : BoolExpr
    {
        internal readonly List<BoolExpr> elements;

        private OrExpr(List<BoolExpr> _elems)
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
                else if (ReferenceEquals(es, TRUE))
                    return TRUE;
                else if (es is OrExpr)
                    res.AddRange(((OrExpr)es).elements);
                else if (!ReferenceEquals(es, FALSE))
                    res.Add(es);

            //remove duplicates
            for (var i = 0; i < res.Count; i++)
                for (var j = i + 1; j < res.Count; j++)
                    if (ReferenceEquals(res[i], res[j]))
                    {
                        res.RemoveAt(j);
                        j--;
                    }

            if (res.Count==0)
                return FALSE;
            else if (res.Count == 1)
                return res.Single();
            else
            {
                /*foreach (var subE in res)
                    if (subE is NotExpr && res.Contains(((NotExpr)subE).inner))
                        return TRUE;*/

                for(var i=0;i<res.Count;i++)
                    if (res[i] is AndExpr)
                    {
                        var andExpr = (AndExpr)res[i];
                        var orExprs = new BoolExpr[andExpr.elements.Length];
                        for (var j = 0; j < orExprs.Length; j++)
                        {
                            res[i] = andExpr.elements[j];
                            orExprs[j] = OrExpr.Create(res);
                        }

                        return AndExpr.Create(orExprs);
                    }

                //find v !a
                foreach(var e in res)
                    if (e is NotExpr && res.Contains(((NotExpr)e).inner))
                        return TRUE;

                res.Sort((a,b) => a.GetHashCode().CompareTo(b.GetHashCode()));
                return new OrExpr(res);
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

        /*public override bool Equals(object _obj)
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
                hashCode = (int)Model.RotateLeft((uint)elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b),4) ^ (1 << 29);
                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }*/
    }
}
