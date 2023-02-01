using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Unicode;

namespace Tests
{
    [TestClass]
    public class DimacsTests
    {
        [TestMethod]
        public void Basic()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                EnableDIMACSWriting = true
            });

            var v = m.AddVars(10);
            m.AddConstr(v[0]);
            m.AddConstr(v[1]);
            m.AddConstr(!v[1]);
            m.AddConstr(v[0] | !v[1]);
            m.AddConstr(v[2] & v[3]);
            m.AddConstr(v[9]);

            using var ms = new MemoryStream();
            using (var sw = new StreamWriter(ms))
                    m.Write(sw);

            Assert.AreEqual("c Created by SATInterface\r\np cnf 10 7\r\n1 0\r\n2 0\r\n-2 0\r\n-2 1 0\r\n3 0\r\n4 0\r\n10 0\r\n", Encoding.UTF8.GetString(ms.ToArray()));
        }

        [TestMethod]
        public void MultipleWrites()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                EnableDIMACSWriting = true
            });

            var v = m.AddVars(10);
            m.AddConstr(v[0]);

            using var ms1 = new MemoryStream();
            using (var sw1 = new StreamWriter(ms1))
                m.Write(sw1);

            m.AddConstr(v[1]);

            using var ms2 = new MemoryStream();
            using (var sw2 = new StreamWriter(ms2))
                m.Write(sw2);

            Assert.AreEqual("c Created by SATInterface\r\np cnf 10 1\r\n1 0\r\n", Encoding.UTF8.GetString(ms1.ToArray()));
            Assert.AreEqual("c Created by SATInterface\r\np cnf 10 2\r\n1 0\r\n2 0\r\n", Encoding.UTF8.GetString(ms2.ToArray()));
        }

        [TestMethod]
        public void EmptyModel()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                EnableDIMACSWriting = true
            });

            var v = m.AddVars(10);

            using var ms = new MemoryStream();
            using (var sw = new StreamWriter(ms))
                m.Write(sw);

            Assert.AreEqual("c Created by SATInterface\r\np cnf 10 0\r\n", Encoding.UTF8.GetString(ms.ToArray()));
        }
    }
}
