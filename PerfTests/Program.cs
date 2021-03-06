﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Innovator.Client;
using System.Diagnostics;
using System.Xml.Linq;
using System.IO;

namespace PerfTests
{
  class Program
  {
    static void Main(string[] args)
    {
      Stopwatch st;

      var assembly = System.Reflection.Assembly.GetExecutingAssembly();
      var resourceName = "PerfTests.ItemTypeAml.xml";
      string itemTypeAml;
      using (var stream = assembly.GetManifestResourceStream(resourceName))
      using (var reader = new StreamReader(stream))
      {
        itemTypeAml = reader.ReadToEnd();
      }

      var item1 = ElementFactory.Local.FromXml(itemTypeAml, "asdf", null);
      //using (var xml = System.Xml.XmlWriter.Create(@"C:\Users\eric.domke\Documents\Code\InnovatorAdmin\PerfTests\Out.xml",
      //  new System.Xml.XmlWriterSettings()
      //  {
      //    OmitXmlDeclaration = true,
      //    Indent = true,
      //    IndentChars = "  "
      //  }))
      //{
      //  item1.ToAml(xml);
      //}
      var item2 = new System.Xml.XmlDocument();
      item2.LoadXml(itemTypeAml);
      var item3 = XDocument.Parse(itemTypeAml);
      Console.WriteLine("Done");
      Console.ReadKey();
      return;

      var readKey = ConsoleKey.A;
      while (readKey != ConsoleKey.Enter)
      {
        for (var i = 0; i < 5; i++)
        {
          ElementFactory.Local.FromXml(itemTypeAml);
        }

        GC.Collect();

        st = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
          var doc = XDocument.Parse(itemTypeAml);
        }
        var baseline = st.ElapsedMilliseconds;
        Console.WriteLine();
        Console.Write("XDocument, {0:D5}ms, 100%", baseline);

        GC.Collect();

        st = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
          var doc = new System.Xml.XmlDocument();
          doc.LoadXml(itemTypeAml);
        }
        Console.Write(", XmlDocument, {0:0.0}%", st.ElapsedMilliseconds * 100.0 / baseline);

        GC.Collect();

        st = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
          ElementFactory.Local.FromXml(itemTypeAml);
        }
        Console.Write(", New, {0:0.0}%", st.ElapsedMilliseconds * 100.0 / baseline);
        Console.WriteLine();

        readKey = Console.ReadKey().Key;
      }
    }
  }
}
