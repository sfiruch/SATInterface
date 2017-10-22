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

            m.AddConstr((x < 512) | (y < 100));

            m.LogOutput = false;
            m.Maximize(x + y, () => Console.WriteLine($"Intermediate result: {x.X} + {y.X} = {x.X + y.X}"));

            Console.WriteLine($"{x.X} + {y.X} = {x.X + y.X}");
            Console.ReadLine();
        }
    }
}
