using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SATInterface
{
    internal class AndExpr : BoolExpr //where T : struct, IBinaryInteger<T>
    {
        internal readonly BoolExpr[] Elements;

        private AndExpr(BoolExpr[] _elems)
        {
            Elements = _elems;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(2);
            arr[0] = _a;
            arr[1] = _b;
            var result = Create(arr.AsSpan()[..2]);
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b, BoolExpr _c)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(3);
            arr[0] = _a;
            arr[1] = _b;
            arr[2] = _c;
            var result = Create(arr.AsSpan()[..3]);
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b, BoolExpr _c, BoolExpr _d)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(4);
            arr[0] = _a;
            arr[1] = _b;
            arr[2] = _c;
            arr[3] = _d;
            var result = Create(arr.AsSpan()[..4]);
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b, BoolExpr _c, BoolExpr _d, BoolExpr _e)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(5);
            arr[0] = _a;
            arr[1] = _b;
            arr[2] = _c;
            arr[3] = _d;
            arr[4] = _e;
            var result = Create(arr.AsSpan()[..5]);
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(ReadOnlySpan<BoolExpr> _elems)
        {
            if (_elems.Length == 0)
                return Model.True;
            if (_elems.Length == 1)
                return _elems[0];

            var count = 0;
            foreach (var es in _elems)
            {
                ArgumentNullException.ThrowIfNull(es, nameof(_elems));
                if (ReferenceEquals(es, Model.False))
                    return Model.False;
                else if (es is AndExpr ae)
                    count += ae.Elements.Length;
                else if (!ReferenceEquals(es, Model.True))
                    count++;
            }

            if (count == 0)
                return Model.True;

            var res = new BoolExpr[count];
            count = 0;
            foreach (var es in _elems)
                if (es is AndExpr ae)
                    foreach (var e in ae.Elements)
                        res[count++] = e;
                else if (!ReferenceEquals(es, Model.True))
                    res[count++] = es;

            Debug.Assert(count == res.Length);

            if (count == 1)
                return res[0];

            var varIds = ArrayPool<int>.Shared.Rent(count);
            try
            {
                var i = 0;
                foreach (var e in res)
                    if (e is BoolVar bv)
                        varIds[i++] = bv.Id;

                if (i > 1)
                {
                    Array.Sort(varIds, 0, i);

                    var negIndex = 0;
                    var posIndex = i - 1;
                    if (varIds[negIndex] < 0 && varIds[posIndex] > 0)
                        for (; ; )
                        {
                            Debug.Assert(varIds[negIndex] < 0);
                            Debug.Assert(varIds[posIndex] > 0);

                            if (-varIds[negIndex] > varIds[posIndex])
                            {
                                negIndex++;
                                if (varIds[negIndex] > 0)
                                    break;
                            }
                            else if (-varIds[negIndex] < varIds[posIndex])
                            {
                                posIndex--;
                                if (varIds[posIndex] < 0)
                                    break;
                            }
                            else
                                return Model.False;
                        }
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(varIds);
            }

            if (res.Length < 10)
            {
                for (var i = 0; i < res.Length; i++)
                    for (var j = i + 1; j < res.Length; j++)
                        if (res[j].Equals(res[i]))
                            return new AndExpr(res.Distinct().ToArray());
                return new AndExpr(res);
            }
            else
            {
                return new AndExpr(res.Distinct().ToArray());
            }
        }

        public override BoolExpr Flatten()
        {
            var be = ArrayPool<BoolExpr>.Shared.Rent(Elements.Length);
            for (var i = 0; i < Elements.Length; i++)
                be[i] = !Elements[i];
            var res = !(OrExpr.Create(be.AsSpan()[..Elements.Length]).Flatten());
            ArrayPool<BoolExpr>.Shared.Return(be);
            return res;
        }

        internal override Model? GetModel()
        {
            foreach (var e in Elements)
                if (e.GetModel() is Model m)
                    return m;

            return null;
        }

        public override string ToString() => "(" + string.Join(" & ", Elements.Select(e => e.ToString()).ToArray()) + ")";

        public override bool X => Elements.All(e => e.X);

        public override int VarCount => Elements.Length;

        public override bool Equals(object? _obj)
        {
            if (_obj is not AndExpr other)
                return false;

            if (Elements.Length != other.Elements.Length)
                return false;

            //as elements are distinct by construction, one-sided comparison is enough
            foreach (var a in Elements)
                if (!other.Elements.Contains(a))
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
                foreach (var e in Elements)
                    hashCode += HashCode.Combine(e);

                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
