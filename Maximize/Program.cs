using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Maximize
{
    class Program
    {
        static void Main(string[] args)
        {
            var m = new Model();

            var x = new UIntVar(m, 1000);
            var y = new UIntVar(m, 200);
            var c = new UIntVar(m, 1000*200);

            m.AddConstr((x < 512) | (y < 100));
            m.AddConstr(c == x*y);

            m.LogOutput = false;
            m.Maximize(x + 7 * y, () => Console.WriteLine($"Intermediate result: {x.X} + 7*{y.X} = {x.X + 7 * y.X}, x*y = {c.X}"), Model.OptimizationStrategy.BinarySearch);

            Console.WriteLine($"Final result: {x.X} + 7*{y.X} = {x.X + 7*y.X}, x*y = {c.X}");
            Console.ReadLine();
        }
    }
}
