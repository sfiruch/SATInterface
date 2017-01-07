using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class AndExpr : BoolExpr
    {
        internal readonly BoolExpr[] elements;

        private AndExpr(BoolExpr[] _elems)
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
                else if (ReferenceEquals(es, FALSE))
                    return FALSE;
                else if (es is AndExpr)
                    res.AddRange(((AndExpr)es).elements);
                else if (!ReferenceEquals(es, TRUE))
                    res.Add(es);

            //remove duplicates
            /*for (var i = 0; i < res.Count; i++)
                for (var j = i + 1; j < res.Count; j++)
                    if (ReferenceEquals(res[i], res[j]))
                    {
                        res.RemoveAt(j);
                        j--;
                    }*/

            if (!res.Any())
                return TRUE;
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

        /*public override bool Equals(object _obj)
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
                hashCode = (int)Model.RotateLeft((uint)elements.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b), 8) ^ (1 << 26);
                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }*/
    }
}
