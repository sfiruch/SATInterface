using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;


namespace SATInterface
{
	/// <summary>
	/// A LinExpr is a linear combination of BoolVars with integer weights.
	/// </summary>
	public class LinExpr //<T> where T:struct,IBinaryInteger<T>
	{
		/// <summary>
		/// Invariant: VarId>0, weights may be negative
		/// </summary>
		internal Dictionary<VarId, T> Weights;
		public T Offset { get; private set; }

		private Model? Model;
		private LinExpr? Negated;

		public IEnumerable<(BoolExpr Var, T Weight)> Terms => Weights.Select(w => ((BoolExpr)new BoolVar(Model!, w.Key), w.Value));

		/// <summary>
		/// Creates a new linear expression
		/// </summary>
		public LinExpr()
		{
			Weights = new();
			Offset = T.Zero;
		}

		/// <summary>
		/// Creates a new linear expression representing an integer constant (default 0)
		/// </summary>
		public LinExpr(T _c)
		{
			Weights = new();
			Offset = _c;
		}

		internal LinExpr(UIntVar _src)
		{
			Model = _src.Model;

			Weights = new();
			Offset = T.Zero;
			for (var i = 0; i < _src.Bits.Length; i++)
				AddTerm(_src.bit[i], T.One << i);
		}

		/// <summary>
		/// Returns the upper bound of this expression.
		/// </summary>
		public T UB
		{
			get
			{
				checked
				{
					var res = Offset;
					foreach (var e in Weights)
						if (e.Value > T.Zero)
							res += e.Value;
					return res;
				}
			}
		}

		/// <summary>
		/// Returns the lower bound of this expression.
		/// </summary>
		public T LB
		{
			get
			{
				checked
				{
					var res = Offset;
					foreach (var e in Weights)
						if (e.Value < T.Zero)
							res += e.Value;
					return res;
				}
			}
		}

		/// <summary>
		/// Returns the number of variables.
		/// </summary>
		public int Size => Weights.Count;

		/// <summary>
		/// Returns the value of this expression in a SAT model.
		/// </summary>
		public T X
		{
			get
			{
				checked
				{
					var res = Offset;
					foreach (var e in Weights)
					{
						Debug.Assert(Model is not null);
						if (Model.GetAssignment(e.Key))
							res += e.Value;
					}
					return res;
				}
			}
		}

		public static LinExpr operator *(T _a, LinExpr _b) => _b * _a;
		public static LinExpr operator *(LinExpr _a, T _b)
		{
			var res = new LinExpr()
			{
				Model = _a.Model
			};
			if (_b == T.Zero)
				return res;

			res.Offset = _a.Offset * _b;
			foreach (var w in _a.Weights)
				res[w.Key] += w.Value * _b;
			return res;
		}

		public static LinExpr operator +(T _a, LinExpr _b) => _b + _a;
		public static LinExpr operator +(LinExpr _a, T _b)
		{
			if (_b == T.Zero)
				return _a;

			var res = new LinExpr()
			{
				Model = _a.Model,
				Offset = _a.Offset + _b
			};
			foreach (var w in _a.Weights)
				res[w.Key] += w.Value;
			return res;
		}

		public static LinExpr operator +(LinExpr _a, LinExpr _b)
		{
			Debug.Assert(ReferenceEquals(_a.Model, _b.Model) || _a.Model is null || _b.Model is null);

			if (_a.Weights.Count < _b.Weights.Count)
				(_a, _b) = (_b, _a);

			var res = new LinExpr()
			{
				Offset = _a.Offset + _b.Offset,
				Model = _a.Model ?? _b.Model,
				Weights = new(_a.Weights)
			};
			res.Weights.EnsureCapacity(res.Weights.Count + _b.Weights.Count);
			foreach (var w in _b.Weights)
				res[w.Key] += w.Value;
			return res;
		}

		public static LinExpr operator -(LinExpr _a, LinExpr _b)
		{
			Debug.Assert(ReferenceEquals(_a.Model, _b.Model));
			var res = new LinExpr()
			{
				Offset = _a.Offset - _b.Offset,
				Model = _a.Model
			};
			foreach (var w in _a.Weights)
				res[w.Key] += w.Value;
			foreach (var w in _b.Weights)
				res[w.Key] -= w.Value;
			return res;
		}

		public static LinExpr operator -(LinExpr _a, T _b)
		{
			if (_b == T.Zero)
				return _a;

			var res = new LinExpr()
			{
				Offset = _a.Offset - _b,
				Model = _a.Model
			};
			foreach (var w in _a.Weights)
				res[w.Key] += w.Value;
			return res;
		}

		public static LinExpr operator -(LinExpr _a)
		{
			if (_a.Negated is not null)
				return _a.Negated;

			var res = new LinExpr()
			{
				Offset = -_a.Offset,
				Model = _a.Model
			};
			foreach (var w in _a.Weights)
				res[w.Key] -= w.Value;
			return _a.Negated = res;
		}

		public static LinExpr operator -(T _a, LinExpr _b)
		{
			if (_a == T.Zero)
				return -_b;

			var res = new LinExpr()
			{
				Offset = _a - _b.Offset,
				Model = _b.Model
			};
			foreach (var w in _b.Weights)
				res[w.Key] -= w.Value;
			return res;
		}

		public static BoolExpr operator <(LinExpr _a, LinExpr _b) => (_a - _b) < T.Zero;
		public static BoolExpr operator <=(LinExpr _a, LinExpr _b) => (_a - _b) <= T.Zero;
		public static BoolExpr operator !=(LinExpr _a, LinExpr _b) => (_a - _b) != T.Zero;
		public static BoolExpr operator >(LinExpr _a, LinExpr _b) => (_a - _b) > T.Zero;
		public static BoolExpr operator >=(LinExpr _a, LinExpr _b) => (_a - _b) >= T.Zero;
		public static BoolExpr operator ==(LinExpr _a, LinExpr _b) => (_a - _b) == T.Zero;

		public static BoolExpr operator <(T _a, LinExpr _b) => _b > _a;
		public static BoolExpr operator <=(T _a, LinExpr _b) => _b >= _a;
		public static BoolExpr operator !=(T _a, LinExpr _b) => _b != _a;
		public static BoolExpr operator >(T _a, LinExpr _b) => _b < _a;
		public static BoolExpr operator >=(T _a, LinExpr _b) => _b <= _a;
		public static BoolExpr operator ==(T _a, LinExpr _b) => _b == _a;

		public static BoolExpr operator <(LinExpr _a, T _b) => _a <= (_b - T.One);

		/// <summary>
		/// Converts to UInt, ignoring offsets and using all-positive weights.
		/// </summary>
		/// <returns></returns>
		internal UIntVar ToUInt(Model _m)
		{
			Debug.Assert(Model is null || ReferenceEquals(Model, _m));

			if (Model.UIntCache.TryGetValue(this, out var res))
				return res;

			//convert to all-positive weights
			var allOneWeights = true;
			var posWeights = new List<(T Weight, BoolExpr Var)>();
			foreach (var e in Weights)
			{
				if (e.Value > T.Zero)
					posWeights.Add((e.Value, Model.GetVariable(e.Key)));
				else
				{
					Debug.Assert(e.Value < T.Zero);
					posWeights.Add((-e.Value, Model.GetVariable(-e.Key)));
				}

				if (T.Abs(e.Value) != T.One)
					allOneWeights = false;
			}

			if (allOneWeights)
				return Model.UIntCache[this] = Model.SumUInt(posWeights.Select(w => w.Var));

			var toSum = new List<UIntVar>();
			var minWeightVars = new List<BoolExpr>();
			while (posWeights.Any())
			{
				T? minWeight = null;
				foreach (var pw in posWeights)
				{
					if (minWeight is null || pw.Weight < minWeight)
					{
						minWeight = pw.Weight;
						minWeightVars.Clear();
					}
					if (pw.Weight == minWeight)
						minWeightVars.Add(pw.Var);
				}

				Debug.Assert(minWeight is not null);
				posWeights.RemoveAll(pw => pw.Weight == minWeight);

				if (minWeightVars.Count == 1)
					toSum.Add(Model.AddUIntConst(minWeight.Value) * minWeightVars.Single());
				else
				{
					var sum = Model.SumUInt(CollectionsMarshal.AsSpan(minWeightVars));
					if (sum.UB > T.Zero)
					{
						if (minWeight.Value.IsPowerOfTwo)
							toSum.Add(sum << (int)T.Log2(minWeight.Value));
						else
						{
							for (var i = 1; i < sum.Bits.Length; i++)
							{
								Debug.Assert((minWeight << i) > T.Zero);
								posWeights.Add((minWeight.Value << i, sum.Bits[i]));
							}

							toSum.Add(Model.AddUIntConst(minWeight.Value) * sum.Bits[0]);
						}
					}
				}
			}

			return Model.UIntCache[this] = Model.Sum(CollectionsMarshal.AsSpan(toSum));
		}

		private void ClearCached()
		{
			Negated = null;
		}

		private static T GCD(T a, T b) => b == T.Zero ? a : GCD(b, a % b);

		public static BoolExpr operator <=(LinExpr _a, T _b)
		{
			if (_a.UB <= _b)
				return Model.True;
			if (_a.LB > _b)
				return Model.False;

			Debug.Assert(_a.Model is not null);

			if (!_a.Model.LinExprLECache.TryGetValue((_a, -_a.Offset + _b), out var res))
			{
				//Debug.WriteLine($"{_a} <= {_b}");
				//Debug.Indent();
				_a.Model.LinExprLECache[(_a, -_a.Offset + _b)] = res = UncachedLE(_a, _b);
				//Debug.Unindent();
			}
			else
			{
				//Debug.WriteLine($"Cached: {_a} <= {_b}");
			}

			return res;
		}

		private static BoolExpr UncachedLE(LinExpr _a, T _b)
		{
			Debug.Assert(_a.Model is not null);

			var rhs = _b - _a.Offset;
			var ub = T.Zero;
			foreach (var e in _a.Weights)
				checked
				{
					ub += T.Abs(e.Value);
					if (e.Value < T.Zero)
						rhs -= e.Value;
				}

			if (_a.Model.UIntCache.TryGetValue(_a, out var uintV))
				return uintV <= rhs;

			Debug.Assert(rhs >= T.Zero);
			Debug.Assert(ub > rhs);

			if (rhs == T.Zero)
				return AndExpr.Create(_a.Weights.Select(w => _a.Model.GetVariable(w.Value > T.Zero ? -w.Key : w.Key)).ToArray());

			if (_a.Model.LinExprLECache.TryGetValue((_a, -_a.Offset + _b - T.One), out var res1) && _a.Model.LinExprEqCache.TryGetValue((_a, -_a.Offset + _b), out var res2))
				return res1 | res2;

			var gcd = _a.Weights.Values.Select(w => T.Abs(w)).Aggregate(GCD);
			if (gcd > T.One)
			{
				var vDiv = new LinExpr()
				{
					Model = _a.Model
				};
				vDiv.Weights.EnsureCapacity(_a.Weights.Count);
				foreach (var w in _a.Weights)
					if (w.Value > T.Zero)
						vDiv.AddTerm(_a.Model.GetVariable(w.Key), w.Value / gcd);
					else if (w.Value < T.Zero)
						vDiv.AddTerm(_a.Model.GetVariable(-w.Key), -w.Value / gcd);

				//Debug.WriteLine($"GCD={gcd}");
				return vDiv <= rhs / gcd;
			}

			//if (_a.Weights.Count <= 18)
			//    //(_a, rhs) = CanonicalizeLE(_a, rhs);
			//    (_a, rhs) = CanonicalizeLE2(_a, rhs);

			var absEqRHSCnt = 0;
			var maxVar = new KeyValuePair<int, T>(0, T.Zero);
			var maxVarCnt = 0;
			T? minAbsWeight = null;
			foreach (var w in _a.Weights)
			{
				var absWeight = T.Abs(w.Value);
				if (absWeight == rhs)
					absEqRHSCnt++;
				if (absWeight > T.Abs(maxVar.Value))
				{
					maxVar = w;
					maxVarCnt = 0;
				}
				if (absWeight == T.Abs(maxVar.Value))
					maxVarCnt++;
				if (minAbsWeight is null || absWeight < minAbsWeight)
					minAbsWeight = absWeight;
				if (absWeight > rhs)
				{
					//TODO: simplify
					var vWithout = new LinExpr()
					{
						Model = _a.Model
					};
					foreach (var wi in _a.Weights)
						if (wi.Value > T.Zero && wi.Value <= rhs)
							vWithout.AddTerm(_a.Model.GetVariable(wi.Key), wi.Value);
						else if (wi.Value < T.Zero && -wi.Value <= rhs)
							vWithout.AddTerm(_a.Model.GetVariable(-wi.Key), -wi.Value);

					return _a.Model.And(_a.Weights
						.Where(w => T.Abs(w.Value) > rhs)
						.Select(w => _a.Model.GetVariable(w.Value > T.Zero ? -w.Key : w.Key))
						.Append(vWithout <= rhs));
				}
			}

			Debug.Assert(_a.Weights.All(w => T.Abs(w.Value) <= rhs));

			if (rhs == minAbsWeight)
			{
				Debug.Assert(_a.Weights.All(w => T.Abs(w.Value) == rhs));
				return _a.Model.AtMostOneOf(_a.Weights.Select(w => _a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)));
			}

			if (absEqRHSCnt >= 1)
			{
				var lWithout = new List<BoolExpr>();
				var vWithout = new LinExpr()
				{
					Model = _a.Model
				};
				foreach (var w in _a.Weights)
					if (T.Abs(w.Value) != rhs)
					{
						vWithout.AddTerm(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key), T.Abs(w.Value));
						lWithout.Add(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key));
					}

				var vRHS = _a.Weights.Where(v => T.Abs(v.Value) == rhs).Select(v => v.Value > T.Zero ? _a.Model.GetVariable(v.Key) : _a.Model.GetVariable(-v.Key)).ToArray();

				Debug.Assert(vWithout.Weights.Count >= 0);

				return (_a.Model.AtMostOneOf(vRHS) & !_a.Model.Or(lWithout)).Flatten()
					| (!_a.Model.Or(vRHS) & vWithout <= rhs).Flatten();
			}

			if (rhs > ub >> 1)
			{
				//Debug.WriteLine($"Swapping to !({-_a} <= {(-_b - 1)})");
				return !(-_a <= (-_b - T.One));
			}

			Debug.Assert(_a.Weights.Count > 2);

			if (T.Abs(maxVar.Value) == T.One && _a.Weights.Count <= 18)
			{
				var res = EnumerateLEResolvent(_a, rhs);
				if (res is not null)
				{
					//Debug.WriteLine($"Enumeration");
					return res;
				}
			}
			else if (_a.Weights.Count <= 18)
			{
				//not all variables have the same weight, ensured by /gcd
				Debug.Assert(T.Abs(maxVar.Value) != minAbsWeight);

				var res = EnumerateLEResolventWeightGrouped(_a, rhs);
				if (res is not null)
				{
					//Debug.WriteLine($"Enumeration");
					return res;
				}
			}

			if (ub <= T.CreateChecked(_a.Model.Configuration.LinExprKOfLimit))
				return _a.Model.AtMostKOf(_a.Weights.SelectMany(w => Enumerable.Repeat(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key), int.CreateChecked(T.Abs(w.Value)))), int.CreateChecked(rhs), Model.KOfMethod.SortTotalizer);

			if (rhs <= T.CreateChecked(_a.Model.Configuration.LinExprKOfLimit))
				return _a.Model.AtMostKOf(_a.Weights.SelectMany(w => Enumerable.Repeat(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key), int.CreateChecked(T.Abs(w.Value)))), int.CreateChecked(rhs), Model.KOfMethod.Sequential);

			if (false && maxVarCnt < 16 && (ub - (T.Abs(maxVar.Value) * (maxVarCnt + 1)) <= rhs))
			{
				var maxVars = new List<BoolExpr>(maxVarCnt);
				var withoutMaxVars = new LinExpr();
				foreach (var e in _a.Weights)
					if (e.Value == T.Abs(maxVar.Value))
						maxVars.Add(_a.Model.GetVariable(e.Key));
					else if (-e.Value == T.Abs(maxVar.Value))
						maxVars.Add(_a.Model.GetVariable(-e.Key));
					else
						withoutMaxVars.AddTerm(_a.Model.GetVariable(e.Value > T.Zero ? e.Key : -e.Key), T.Abs(e.Value));

				Debug.Assert(maxVars.Count == maxVarCnt);

				var sorted = _a.Model.SortTotalizer(CollectionsMarshal.AsSpan(maxVars));
				return _a.Model.Or(Enumerable.Range(0, maxVarCnt + 1).Select(s => (s == maxVarCnt ? Model.True : !sorted[s]) & (withoutMaxVars + s * T.Abs(maxVar.Value) <= rhs).Flatten())).Flatten();
			}
			else
			{
				//TODO: implement section 5 with r=2k and c=1 of Ben-Haim, Y., Ivrii, A., Margalit, O. and Matsliah, A., 2012, June. Perfect hashing and CNF encodings of cardinality constraints. In International Conference on Theory and Applications of Satisfiability Testing (pp. 397-409). Berlin, Heidelberg: Springer Berlin Heidelberg.

				//Debug.WriteLine($"Binary");
				return _a.ToUInt(_a.Model) <= rhs;
			}
		}

		public static BoolExpr operator ==(LinExpr _a, T _b)
		{
			if (_a.UB < _b || _a.LB > _b)
				return Model.False;

			if (_a.LB == _a.UB)
				return _a.LB == _b ? Model.True : Model.False;

			Debug.Assert(_a.Model is not null);

			if (!_a.Model.LinExprEqCache.TryGetValue((_a, -_a.Offset + _b), out var res))
			{
				//Debug.WriteLine($"{_a} == {_b}:");
				//Debug.Indent();
				_a.Model.LinExprEqCache[(_a, -_a.Offset + _b)] = res = UncachedEq(_a, _b);
				//Debug.Unindent();
			}
			else
			{
				//Debug.WriteLine($"{_a} == {_b}: Cached");
			}

			return res;
		}


		private static BoolExpr? EnumerateLEResolvent(LinExpr _a, T _rhs)
		{
			var m = _a.Model!;
			var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
			if (limit == 0)
				return null;

			var vars = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => _a.Model!.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)).ToArray();
			var weights = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => T.Abs(w.Value)).ToArray();

			var resolvent = new List<BoolExpr[]>(limit + 1);
			var active = new Stack<BoolExpr>(vars.Length);
			void Visit(int _s)
			{
				if (resolvent.Count > limit)
					return;

				active.Push(vars[_s]);
				_rhs -= weights[_s];

				if (_rhs < T.Zero)
					resolvent.Add(active.ToArray());
				else
					for (var i = _s + 1; i < weights.Length; i++)
						Visit(i);

				active.Pop();
				_rhs += weights[_s];
			}

			for (var i = 0; i < weights.Length; i++)
				Visit(i);

			Debug.Assert(active.Count == 0);

			if (resolvent.Count > limit)
				return null;
			else
				return AndExpr.Create(resolvent.Select(r => OrExpr.Create(r.Select(v => !v).ToArray()).Flatten()).ToArray());
		}

		private static BoolExpr? EnumerateLEResolventWeightGrouped(LinExpr _a, T _rhs)
		{
			var m = _a.Model!;
			var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
			if (limit == 0)
				return null;

			var weights = _a.Weights.Values.Select(w => T.Abs(w)).Distinct().OrderByDescending(w => w).ToArray();
			var max = weights.Select(wi => _a.Weights.Count(w => T.Abs(w.Value) == wi)).ToArray();
			var count = new int[weights.Length];

			var resolvent = new List<int[]>(limit + 1);
			void Visit(int _s)
			{
				if (resolvent.Count > limit)
					return;

				if (_s + 1 < weights.Length)
					Visit(_s + 1);

				var origRHS = _rhs;
				for (count[_s] = 1; count[_s] <= max[_s]; count[_s]++)
				{
					_rhs -= weights[_s];
					if (_rhs < T.Zero)
					{
						resolvent.Add(count.ToArray());
						break;
					}
					else if (_rhs >= T.Zero && _s + 1 < weights.Length)
						Visit(_s + 1);

				}

				_rhs = origRHS;
				count[_s] = 0;
			}

			Visit(0);

			if (resolvent.Count > limit)
				return null;

			var varCnt = weights.Select(wi => m.Sum(_a.Weights.Where(w => T.Abs(w.Value) == wi)
				.Select(w => m.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)))).ToArray();

			return AndExpr.Create(resolvent.Select(a => OrExpr.Create(
					a.Select((cnt, i) => varCnt[i] < T.CreateChecked(cnt)).ToArray()
				)).ToArray());
		}


		private static BoolExpr? EnumerateEqAssignments(LinExpr _a, T _rhs)
		{
			var m = _a.Model!;
			var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
			if (limit == 0)
				return null;

			var vars = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => m.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)).ToArray();
			var _weights = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => T.Abs(w.Value)).ToArray();

			var res = new List<BoolExpr[]>(limit + 1);
			var active = new Stack<BoolExpr>(vars.Length);
			void Visit(int _s)
			{
				if (res.Count > limit)
					return;

				active.Push(vars[_s]);
				_rhs -= _weights[_s];

				if (_rhs == T.Zero)
				{
					for (var i = _s + 1; i < _weights.Length; i++)
						active.Push(!vars[i]);
					res.Add(active.ToArray());
					for (var i = _s + 1; i < _weights.Length; i++)
						active.Pop();
				}
				else if (_rhs > T.Zero)
				{
					for (var i = _s + 1; i < _weights.Length; i++)
					{
						Visit(i);
						active.Push(!vars[i]);
					}
					for (var i = _s + 1; i < _weights.Length; i++)
						active.Pop();
				}

				_rhs += _weights[_s];
				active.Pop();
			}

			for (var i = 0; i < _weights.Length; i++)
			{
				Visit(i);
				active.Push(!vars[i]);
			}

			Debug.Assert(active.Count == _weights.Length);

			if (res.Count > limit)
				return null;
			else
				return OrExpr.Create(res.Select(vars => AndExpr.Create(vars).Flatten()).ToArray());
		}

		private static BoolExpr? EnumerateEqAssignmentsWeightGrouped(LinExpr _a, T _rhs)
		{
			var m = _a.Model!;
			var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
			if (limit == 0)
				return null;

			var weights = _a.Weights.Values.Select(w => T.Abs(w)).Distinct().OrderByDescending(w => w).ToArray();
			var max = weights.Select(wi => _a.Weights.Count(w => T.Abs(w.Value) == wi)).ToArray();
			var count = new int[weights.Length];

			var validAssignments = new List<int[]>(limit + 1);
			void Visit(int _s, int _cnt)
			{
				if (validAssignments.Count > limit)
					return;

				count[_s] = _cnt;
				_rhs -= weights[_s] * T.CreateChecked(_cnt);

				if (_rhs == T.Zero)
					validAssignments.Add(count.ToArray());
				else if (_rhs > T.Zero && _s + 1 < weights.Length)
					for (var i = 0; i <= max[_s + 1]; i++)
						Visit(_s + 1, i);

				_rhs += weights[_s] * T.CreateChecked(_cnt);
				count[_s] = 0;
			}

			for (var i = 0; i <= max[0]; i++)
				Visit(0, i);

			if (validAssignments.Count > limit)
				return null;

			var varCnt = weights.Select(wi => m.Sum(_a.Weights.Where(w => T.Abs(w.Value) == wi)
				.Select(w => m.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)))).ToArray();

			return OrExpr.Create(validAssignments.Select(a => AndExpr.Create(
					a.Select((cnt, i) => varCnt[i] == T.CreateChecked(cnt)).ToArray()
				)).ToArray());
		}

		private static (LinExpr LE, T RHS) CanonicalizeLE2(LinExpr _a, T _rhs)
		{
			var m = _a.Model!;
			var abortEarly = false;

			List<int[]> ComputeResolvent(T[] _weights, T _rhs)
			{
				var res = new List<int[]>();
				var active = new Stack<int>();
				var remaining = _rhs;
				void Visit(int _s)
				{
					if (abortEarly)
						return;

					active.Push(_s);
					remaining -= _weights[_s];

					if (remaining < T.Zero)
					{
						var r = new int[active.Count];
						active.CopyTo(r, 0);
						Array.Reverse(r);

						//if (ComputeLBUB(_weights, r).LB == _rhs)
						//  abortEarly = true;

						res.Add(r);
					}
					else
						for (var i = _s + 1; i < _weights.Length; i++)
							Visit(i);

					active.Pop();
					remaining += _weights[_s];
				}

				for (var i = 0; i < _weights.Length; i++)
					Visit(i);

				return res;
			}

			(T LB, T UB) ComputeLBUB(T[] _weights, int[] _cj)
			{
				var lb = T.Zero;
				for (var i = 0; i < _cj.Length - 1; i++)
					lb += _weights[_cj[i]];
				return (lb, lb + _weights[_cj[^1]] - T.One);
			}

			bool IsValid(T[] _oldWeights, T _rhs, T[] _newWeights, T _newRHS)
			{
				var remainingOld = _rhs;
				var remainingNew = _newRHS;

				if (remainingOld < T.Zero ^ remainingNew < T.Zero)
					return false;

				bool Visit(int _s)
				{
					remainingOld -= _oldWeights[_s];
					remainingNew -= _newWeights[_s];

					if (remainingOld < T.Zero ^ remainingNew < T.Zero)
						return false;

					if (remainingOld >= T.Zero && remainingNew >= T.Zero)
						for (var i = _s + 1; i < _oldWeights.Length; i++)
							if (!Visit(i))
								return false;

					remainingOld += _oldWeights[_s];
					remainingNew += _newWeights[_s];
					return true;
				}

				for (var i = 0; i < _oldWeights.Length; i++)
					if (!Visit(i))
						return false;

				return true;
			}

			var vars = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => m.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)).ToArray();
			var oldWeights = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => T.Abs(w.Value)).ToArray();
			var resolvent = ComputeResolvent(oldWeights, _rhs);
			if (abortEarly)
				return (_a, _rhs);

			var nonZero = new bool[vars.Length];
			for (var i = 0; i < vars.Length; i++)
				nonZero[i] = resolvent.Any(r => r.Contains(i));

			var newWeights = new T[vars.Length];
			for (var scale = T.One; scale < oldWeights[0]; scale++)
			{
				for (var i = 0; i < oldWeights.Length; i++)
					if (nonZero[i])
						newWeights[i] = (oldWeights[i] * scale + oldWeights[0] - T.One) / oldWeights[0];

				var minRHS = T.Zero;
				T? maxRHS = null;

				foreach (var r in resolvent)
				{
					(var lb, var ub) = ComputeLBUB(newWeights, r);
					if (lb > minRHS)
						minRHS = lb;
					if (maxRHS is null || ub < maxRHS)
						maxRHS = ub;
				}

				for (var rhs = minRHS; rhs <= maxRHS; rhs++)
					if (IsValid(oldWeights, _rhs, newWeights, rhs))
					{
						var res = new LinExpr()
						{
							Model = _a.Model
						};
						for (var i = 0; i < oldWeights.Length; i++)
							res.AddTerm(vars[i], newWeights[i]);
						return (res, rhs);
					}
			}

			return (_a, _rhs);
		}

		private static (LinExpr LE, T RHS) CanonicalizeLE(LinExpr _a, T _rhs)
		{
			//Wilson, J.M., 1977. A method for reducing coefficients in zero‐one linear
			//inequalities. International Journal of Mathematical Educational in Science
			//and Technology, 8(1), pp.31-35.
			//https://www.tandfonline.com/doi/pdf/10.1080/0020739770080104

			//Onyekwelu, D.C. and Proll, L.G., 1982. On Wilson's method for equivalent
			//inequalities. International Journal of Mathematical Education in Science
			//and Technology, 13(5), pp.551-557.
			//https://www.tandfonline.com/doi/pdf/10.1080/0020739820130505

			var abortEarly = false;
			var m = _a.Model!;

			List<int[]> ComputeResolvent(T[] _weights, T _rhs)
			{
				var res = new List<int[]>();
				var active = new Stack<int>();
				var remaining = _rhs;
				void Visit(int _s)
				{
					if (abortEarly)
						return;

					active.Push(_s);
					remaining -= _weights[_s];

					if (remaining < T.Zero)
					{
						var r = new int[active.Count];
						active.CopyTo(r, 0);
						Array.Reverse(r);

						//if (ComputeLBUB(_weights, r).LB == _rhs)
						//  abortEarly = true;

						res.Add(r);
					}
					else
						for (var i = _s + 1; i < _weights.Length; i++)
							Visit(i);

					active.Pop();
					remaining += _weights[_s];
				}

				for (var i = 0; i < _weights.Length; i++)
					Visit(i);

				return res;
			}

			(T LB, T UB) ComputeLBUB(T[] _weights, int[] _cj)
			{
				var lb = T.Zero;
				for (var i = 0; i < _cj.Length - 1; i++)
					lb += _weights[_cj[i]];
				return (lb, lb + _weights[_cj[^1]] - T.One);
			}

			bool IsValid(T[] _oldWeights, T _rhs, T[] _newWeights, T _newRHS)
			{
				var remainingOld = _rhs;
				var remainingNew = _newRHS;

				if (remainingOld < T.Zero ^ remainingNew < T.Zero)
					return false;

				bool Visit(int _s)
				{
					remainingOld -= _oldWeights[_s];
					remainingNew -= _newWeights[_s];

					if (remainingOld < T.Zero ^ remainingNew < T.Zero)
						return false;

					if (remainingOld >= T.Zero && remainingNew >= T.Zero)
						for (var i = _s + 1; i < _oldWeights.Length; i++)
							if (!Visit(i))
								return false;

					remainingOld += _oldWeights[_s];
					remainingNew += _newWeights[_s];
					return true;
				}

				for (var i = 0; i < _oldWeights.Length; i++)
					if (!Visit(i))
						return false;

				return true;
			}



			var vars = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => m.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)).ToArray();
			var oldWeights = _a.Weights.OrderByDescending(w => T.Abs(w.Value)).Select(w => T.Abs(w.Value)).ToArray();
			var resolvent = ComputeResolvent(oldWeights, _rhs);
			if (abortEarly)
				return (_a, _rhs);

			var largerThan = new bool[vars.Length, vars.Length];
			foreach (var r in resolvent)
				for (var jk = 0; jk < vars.Length; jk++)
				{
					var idx = Array.IndexOf(r, jk);
					if (idx == -1)
						continue;

					for (var jkt = jk + 1; jkt < oldWeights.Length && (idx == r.Length - 1 || jkt < r[idx + 1]); jkt++)
					{
						Debug.Assert(!r.Contains(jkt));

						var r2 = r.ToArray();
						r2[idx] = jkt;
						Array.Sort(r2);

						if (!resolvent.Any(rnot => Enumerable.SequenceEqual(rnot, r2)))
						{
							largerThan[jk, jkt] = true;
							break;
						}
					}
				}


			var newWeights = new T[vars.Length];
			for (var i = 0; i < vars.Length; i++)
				if (resolvent.Any(r => r.Contains(i)))
					newWeights[i] = T.One;

			var increased = new bool[vars.Length];
			void ApplyRules()
			{
				//TODO: in reverse order - things should converge in 1 step?
				for (; ; )
				{
					var changed = false;
					for (var i = 0; i < vars.Length; i++)
						for (var j = i + 1; j < vars.Length; j++)
							if (oldWeights[i] == oldWeights[j] && newWeights[i] < newWeights[j])
							{
								increased[i] = true;
								newWeights[i] = newWeights[j];
								changed = true;
							}
							else if (largerThan[i, j] && newWeights[i] <= newWeights[j])
							{
								increased[i] = true;
								newWeights[i] = newWeights[j] + T.One;
								changed = true;
							}

					if (!changed)
						break;
				}
			}

			ApplyRules();

			for (; ; )
			{
				var minRHS = T.Zero;
				T? maxRHS = null;

				foreach (var r in resolvent)
				{
					(var lb, var ub) = ComputeLBUB(newWeights, r);
					if (lb > minRHS)
						minRHS = lb;
					if (maxRHS is null || ub < maxRHS)
						maxRHS = ub;
				}

				for (var rhs = minRHS; rhs <= maxRHS; rhs++)
					if (IsValid(oldWeights, _rhs, newWeights, rhs))
					{
						var res = new LinExpr()
						{
							Model = _a.Model
						};
						for (var i = 0; i < oldWeights.Length; i++)
							res.AddTerm(vars[i], newWeights[i]);
						return (res, rhs);
					}



				var incI = Enumerable.Range(0, vars.Length).Where(i => !increased[i]).MaxBy(i => newWeights[i]);
				Array.Clear(increased);
				newWeights[incI]++;
				increased[incI] = true;
				ApplyRules();
			}
		}

		private static BoolExpr UncachedEq(LinExpr _a, T _b)
		{
			Debug.Assert(_a.Model is not null);

			var rhs = _b - _a.Offset;
			var ub = T.Zero;
			foreach (var e in _a.Weights)
				checked
				{
					ub += T.Abs(e.Value);
					if (e.Value < T.Zero)
						rhs -= e.Value;
				}

			if (_a.Model.UIntCache.TryGetValue(_a, out var uintV))
				return uintV == rhs;

			Debug.Assert(rhs >= T.Zero);
			Debug.Assert(ub >= rhs);

			if (rhs == T.Zero)
				return AndExpr.Create(_a.Weights.Select(x => x.Value > T.Zero ? _a.Model.GetVariable(-x.Key) : _a.Model.GetVariable(x.Key)).ToArray());
			if (rhs == ub)
				return AndExpr.Create(_a.Weights.Select(x => x.Value < T.Zero ? _a.Model.GetVariable(-x.Key) : _a.Model.GetVariable(x.Key)).ToArray());

			if (_a.Model.LinExprLECache.TryGetValue((_a, -_a.Offset + _b - T.One), out var res1) && _a.Model.LinExprLECache.TryGetValue((_a, -_a.Offset + _b), out var res2))
				return !res1 & res2;

			var gcd = _a.Weights.Values.Select(w => T.Abs(w)).Aggregate(GCD);
			if (rhs % gcd != T.Zero)
				return Model.False;

			if (gcd > T.One)
			{
				var vDiv = new LinExpr()
				{
					Model = _a.Model
				};
				vDiv.Weights.EnsureCapacity(_a.Weights.Count);
				foreach (var w in _a.Weights)
					if (w.Value > T.Zero)
						vDiv.AddTerm(_a.Model.GetVariable(w.Key), w.Value / gcd);
					else if (w.Value < T.Zero)
						vDiv.AddTerm(_a.Model.GetVariable(-w.Key), -w.Value / gcd);

				//Debug.WriteLine($"GCD={gcd}");
				return vDiv == rhs / gcd;
			}

			var absEqRHSCnt = 0;
			var maxVar = new KeyValuePair<int, T>(0, T.Zero);
			var maxVarCnt = 0;
			T? minAbsWeight = null;
			foreach (var w in _a.Weights)
			{
				var absWeight = T.Abs(w.Value);
				if (absWeight == rhs)
					absEqRHSCnt++;
				if (absWeight > T.Abs(maxVar.Value))
				{
					maxVar = w;
					maxVarCnt = 0;
				}
				if (absWeight == T.Abs(maxVar.Value))
					maxVarCnt++;
				if (minAbsWeight is null || absWeight < minAbsWeight)
					minAbsWeight = absWeight;
				if (absWeight > rhs)
				{
					//TODO: simplify
					var vWithout = new LinExpr()
					{
						Model = _a.Model
					};
					foreach (var wi in _a.Weights)
						if (wi.Value > T.Zero && wi.Value <= rhs)
							vWithout.AddTerm(_a.Model.GetVariable(wi.Key), wi.Value);
						else if (wi.Value < T.Zero && -wi.Value <= rhs)
							vWithout.AddTerm(_a.Model.GetVariable(-wi.Key), -wi.Value);

					return _a.Model.And(_a.Weights
						.Where(w => T.Abs(w.Value) > rhs)
						.Select(w => _a.Model.GetVariable(w.Value > T.Zero ? -w.Key : w.Key))
						.Append(vWithout == rhs));
				}
			}

			Debug.Assert(_a.Weights.All(w => T.Abs(w.Value) <= rhs));

			if (rhs == minAbsWeight)
			{
				Debug.Assert(_a.Weights.All(w => T.Abs(w.Value) == rhs));
				return _a.Model.ExactlyOneOf(_a.Weights.Select(w => _a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key)));
			}

			if (absEqRHSCnt >= 1)
			{
				var lWithout = new List<BoolExpr>();
				var vWithout = new LinExpr();
				foreach (var w in _a.Weights)
					if (T.Abs(w.Value) != rhs)
					{
						vWithout.AddTerm(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key), T.Abs(w.Value));
						lWithout.Add(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key));
					}

				var vRHS = _a.Weights.Where(v => T.Abs(v.Value) == rhs).Select(v => v.Value > T.Zero ? _a.Model.GetVariable(v.Key) : _a.Model.GetVariable(-v.Key)).ToArray();

				Debug.Assert(vWithout.Weights.Count >= 0);

				return (_a.Model.ExactlyOneOf(vRHS) & !_a.Model.Or(lWithout)).Flatten()
					| (!_a.Model.Or(vRHS) & vWithout == rhs).Flatten();
			}

			if (rhs > ub >> 1)
			{
				//Debug.WriteLine($"Swapping to {-_a} == {-_b}");
				return -_a == -_b;
			}

			Debug.Assert(_a.Weights.Count > 2);

			if (T.Abs(maxVar.Value) == T.One && _a.Weights.Count <= 18)
			{
				var res = EnumerateEqAssignments(_a, rhs);
				if (res is not null)
				{
					//Debug.WriteLine($"Enumeration");
					return res;
				}
			}
			else if (_a.Weights.Count <= 18)
			{
				//not all variables have the same weight, should be guaranteed by /gcd
				Debug.Assert(T.Abs(maxVar.Value) != minAbsWeight);

				var res = EnumerateEqAssignmentsWeightGrouped(_a, rhs);
				if (res is not null)
				{
					//Debug.WriteLine($"Enumeration");
					return res;
				}
			}

			if (ub <= T.CreateChecked(_a.Model.Configuration.LinExprKOfLimit))
				return _a.Model.ExactlyKOf(_a.Weights.SelectMany(w => Enumerable.Repeat(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key), int.CreateChecked(T.Abs(w.Value)))), int.CreateChecked(rhs), Model.KOfMethod.SortTotalizer);

			if (rhs <= T.CreateChecked(_a.Model.Configuration.LinExprKOfLimit))
				return _a.Model.ExactlyKOf(_a.Weights.SelectMany(w => Enumerable.Repeat(_a.Model.GetVariable(w.Value > T.Zero ? w.Key : -w.Key), int.CreateChecked(T.Abs(w.Value)))), int.CreateChecked(rhs), Model.KOfMethod.Sequential);

			if (maxVarCnt < 16)
			{
				var maxVars = new List<BoolExpr>(maxVarCnt);
				var withoutMaxVars = new LinExpr();
				foreach (var e in _a.Weights)
					if (e.Value == T.Abs(maxVar.Value))
						maxVars.Add(_a.Model.GetVariable(e.Key));
					else if (-e.Value == T.Abs(maxVar.Value))
						maxVars.Add(_a.Model.GetVariable(-e.Key));
					else
						withoutMaxVars.AddTerm(_a.Model.GetVariable(e.Value > T.Zero ? e.Key : -e.Key), T.Abs(e.Value));

				Debug.Assert(maxVars.Count == maxVarCnt);

				var sorted = _a.Model.SortTotalizer(CollectionsMarshal.AsSpan(maxVars)).ToArray();
				return _a.Model.Or(Enumerable.Range(0, maxVarCnt + 1).Select(s => (s == maxVarCnt ? Model.True : !sorted[s]) & (s == 0 ? Model.True : sorted[s - 1]) & (withoutMaxVars + s * T.Abs(maxVar.Value) == rhs).Flatten())).Flatten();
			}
			else
			{
				//Debug.WriteLine($"Binary");
				return _a.ToUInt(_a.Model) == rhs;
			}
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			foreach (var e in Weights)
			{
				if (sb.Length == 0)
				{
					if (e.Value == -T.One)
						sb.Append('-');
					else if (e.Value != T.One)
						sb.Append(e.Value + "*");
				}
				else
				{
					if (e.Value == -T.One)
						sb.Append(" - ");
					else if (e.Value < T.Zero)
						sb.Append($" - {-e.Value}*");
					else if (e.Value == T.One)
						sb.Append(" + ");
					else if (e.Value > T.One)
						sb.Append($" + {e.Value}*");
					else
						throw new Exception();
				}
				sb.Append($"v{e.Key}");
			}

			if (Offset > T.Zero)
				sb.Append($" + {Offset}");
			else if (Offset < T.Zero)
				sb.Append($" - {-Offset}");

			return sb.ToString();
		}

		public static BoolExpr operator !=(LinExpr _a, T _b) => !(_a == _b);
		public static BoolExpr operator >(LinExpr _a, T _b) => -_a < -_b;
		public static BoolExpr operator >=(LinExpr _a, T _b) => !(_a <= (_b - T.One));
		public static explicit operator LinExpr(T _const) => new() { Offset = _const };

		public static explicit operator LinExpr(BoolExpr _be)
		{
			var le = new LinExpr();
			le.AddTerm(_be);
			return le;
		}

		private T this[VarId _bv]
		{
			get
			{
				Debug.Assert(_bv > 0);
				return Weights.TryGetValue(_bv, out var weight) ? weight : T.Zero;
			}
			set
			{
				Debug.Assert(_bv > 0);
				if (value == T.Zero)
					Weights.Remove(_bv);
				else
					Weights[_bv] = value;
			}
		}

		public void AddTerm(LinExpr _le) => AddTerm(_le, T.One);

		public void AddTerm(LinExpr _le, T _weight)
		{
			Model ??= _le.Model;

			if (_weight == T.Zero)
				return;

			ClearCached();

			Offset += _le.Offset * _weight;

			foreach (var e in _le.Weights)
				this[e.Key] += e.Value * _weight;
		}

		public void AddTerm(BoolExpr _be) => AddTerm(_be, T.One);

		public void AddTerm(BoolExpr _be, T _weight)
		{
			Model ??= _be.GetModel();

			ClearCached();

			var be = _be.Flatten();
			if (ReferenceEquals(be, Model.True))
				Offset += _weight;
			else if (ReferenceEquals(be, Model.False))
			{
				//do nothing
			}
			else if (be is BoolVar bv)
			{
				if (bv.Model is not null)
					Model = bv.Model;

				Debug.Assert(Model is not null);

				if (bv.Id > 0)
				{
					this[bv.Id] += _weight;
				}
				else
				{
					Offset += _weight;
					this[-bv.Id] -= _weight;
				}
			}
			else
				throw new Exception();
		}

		public override int GetHashCode()
		{
			var hc = new HashCode();

			hc.Add(Offset);
			foreach (var e in Weights.OrderBy(e => e.Key))
			{
				hc.Add(e.Key);
				hc.Add(e.Value);
			}

			return hc.ToHashCode();
		}

		public override bool Equals(object? _o)
		{
			if (_o is not LinExpr le)
				return false;

			if (Offset != le.Offset)
				return false;

			if (Weights.Count != le.Weights.Count)
				return false;

			return Weights.OrderBy(e => e.Key).SequenceEqual(le.Weights.OrderBy(e => e.Key));
		}
	}
}
