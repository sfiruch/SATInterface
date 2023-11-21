using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Tests
{
	[TestClass]
	public class AtMostKOfTests
	{
		[DataRow(null)]
		[DataRow(Model.KOfMethod.BinaryCount)]
		[DataRow(Model.KOfMethod.LinExpr)]
		[DataRow(Model.KOfMethod.Sequential)]
		[DataRow(Model.KOfMethod.SortPairwise)]
		[DataRow(Model.KOfMethod.SortTotalizer)]
		[DataTestMethod]
		public void AtMostKPositiveDynamic(Model.KOfMethod? _method)
		{
			for (var size = 0; size < 10; size++)
				for (var k = -1; k < size + 1; k++)
					for (var pos = 0; pos <= size; pos++)
					{
						using var m = new Model();
						var v = m.AddVars(size);

						for (var i = 0; i < size; i++)
							if (i < pos)
								m.AddConstr(v[i]);
							else
								m.AddConstr(!v[i]);

						m.AddConstr(m.AtMostKOf(v,k));
						m.Solve();

						if(pos<=k)
							Assert.AreEqual(State.Satisfiable, m.State);
						else
							Assert.AreEqual(State.Unsatisfiable, m.State);
					}
		}

		[DataRow(null)]
		[DataRow(Model.KOfMethod.BinaryCount)]
		[DataRow(Model.KOfMethod.LinExpr)]
		[DataRow(Model.KOfMethod.Sequential)]
		[DataRow(Model.KOfMethod.SortPairwise)]
		[DataRow(Model.KOfMethod.SortTotalizer)]
		[DataTestMethod]
		public void AtMostKPositiveStatic(Model.KOfMethod? _method)
		{
			for (var size = 0; size < 10; size++)
				for (var k = -1; k < size + 1; k++)
					for (var pos = 0; pos <= size; pos++)
					{
						using var m = new Model();
						var v = new BoolExpr[size];

						for (var i = 0; i < size; i++)
							if (i < pos)
								v[i] = Model.True;
							else
								v[i] = Model.False;

						m.AddConstr(m.AtMostKOf(v, k));
						m.Solve();

						if (pos <= k)
							Assert.AreEqual(State.Satisfiable, m.State);
						else
							Assert.AreEqual(State.Unsatisfiable, m.State);
					}
		}

		[DataRow(null)]
		[DataRow(Model.KOfMethod.BinaryCount)]
		[DataRow(Model.KOfMethod.LinExpr)]
		[DataRow(Model.KOfMethod.Sequential)]
		[DataRow(Model.KOfMethod.SortPairwise)]
		[DataRow(Model.KOfMethod.SortTotalizer)]
		[DataTestMethod]
		public void AtMostKNegativeDynamic(Model.KOfMethod? _method)
		{
			for (var size = 0; size < 10; size++)
				for (var k = -1; k < size + 1; k++)
					for (var pos = 0; pos <= size; pos++)
					{
						using var m = new Model();
						var v = m.AddVars(size);

						for (var i = 0; i < size; i++)
							if (i < pos)
								m.AddConstr(v[i]);
							else
								m.AddConstr(!v[i]);

						m.AddConstr(!m.AtMostKOf(v, k));
						m.Solve();

						if (pos > k)
							Assert.AreEqual(State.Satisfiable, m.State);
						else
							Assert.AreEqual(State.Unsatisfiable, m.State);
					}
		}

		[DataRow(null)]
		[DataRow(Model.KOfMethod.BinaryCount)]
		[DataRow(Model.KOfMethod.LinExpr)]
		[DataRow(Model.KOfMethod.Sequential)]
		[DataRow(Model.KOfMethod.SortPairwise)]
		[DataRow(Model.KOfMethod.SortTotalizer)]
		[DataTestMethod]
		public void AtMostKNegativeStatic(Model.KOfMethod? _method)
		{
			for (var size = 0; size < 10; size++)
				for (var k = -1; k < size + 1; k++)
					for (var pos = 0; pos <= size; pos++)
					{
						using var m = new Model();
						var v = new BoolExpr[size];

						for (var i = 0; i < size; i++)
							if (i < pos)
								v[i] = Model.True;
							else
								v[i] = Model.False;

						m.AddConstr(!m.AtMostKOf(v, k));
						m.Solve();

						if (pos > k)
							Assert.AreEqual(State.Satisfiable, m.State);
						else
							Assert.AreEqual(State.Unsatisfiable, m.State);
					}
		}
	}
}
