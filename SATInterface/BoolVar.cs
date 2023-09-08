using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
	/// <summary>
	/// A BoolVar is either True or False in a SAT model.
	/// </summary>
	public class BoolVar : BoolExpr //<T> : BoolExpr where T: struct, IBinaryInteger<T>
	{
		public readonly int Id;
		internal readonly Model Model;

		internal BoolExpr Negated => new BoolVar(Model, -Id);

		public override BoolExpr Flatten() => this;

		internal BoolVar(Model _model, int _id)
		{
			Debug.Assert(_id != 0);

			Model = _model;
			Id = _id;
		}

		public void SetPhase(bool? _phase) => Model?.Configuration.Solver.SetPhase(Id, _phase);

		public override string ToString()
		{
			if (ReferenceEquals(this, Model.True))
				return "true";
			if (ReferenceEquals(this, Model.False))
				return "false";

			if (Id > 0)
				return $"b{Id}";
			else
				return $"!b{-Id}";
		}

		public override bool X
		{
			get
			{
				if (ReferenceEquals(this, Model.True))
					return true;
				if (ReferenceEquals(this, Model.False))
					return false;

				if (Model!.State == State.Unsatisfiable)
					throw new InvalidOperationException("Model is UNSAT");

				return Model.GetAssignment(Id);
			}
		}

		public override int VarCount => 1;

		internal override Model? GetModel() => Model;

		public override int GetHashCode() => Id;

		public override bool Equals(object? obj) => (obj is BoolVar bv) && bv.Id == Id;
	}
}
