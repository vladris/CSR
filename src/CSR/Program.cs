using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

using CSR.AST;
using CSR.Compiler;
using CSR.Parser;

namespace CSR
{
    class Program
    {
        static void Main(string[] args)
        {
            // A call with no arguments display the help information
            if (args.Length == 0)
            {
                Help();

                return;
            }

            // A call with argument "?" or "help" display the help information
            if (args[0] == "?" || args[0] == "-?" || args[0] == "/?" || args[0].ToLower() == "help" || args[0].ToLower() == "-help" || args[0].ToLower() == "/help")
            {
                Help();

                return;
            }

            // Check if source file exists
            if (!File.Exists(args[0]))
            {
                Console.WriteLine("Source file '{0}' not found", args[0]);
                return;
            }

            // Create compiler object for source file
            CSR.Compiler.Compiler compiler = new CSR.Compiler.Compiler(args[0]);

            // Add mscorlib assembly
            compiler.AddReference("mscorlib.dll");

            // Add any other command line assembly references
            for (int i = 1; i < args.Length; i++)
            {
                compiler.AddReference(args[i]);
            }

            // Compile
            compiler.Compile();
        }

        /// <summary>
        /// Diaplays command line help information
        /// </summary>
        private static void Help()
        {
            ConsoleColor save = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine();
            Console.WriteLine("CSR - Riscutia Vlad, Universitatea de Vest, Timisoara 2007-2008");
            Console.WriteLine("Generarea de cod executabil pe platforma .NET");
            Console.WriteLine();

            Console.ForegroundColor = save;

            Console.WriteLine("Syntax:");
            Console.WriteLine("   CSR <source file> {<assembly>}");
            Console.WriteLine();
            Console.WriteLine("   <source file> - source file to compile");
            Console.WriteLine("   <assembly> - assembly reference to include, use strong name");
            Console.WriteLine("                mscorlib is included automatically");
            Console.WriteLine();
            Console.WriteLine("   Use quotes if parameters contain whitespaces");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("   CSR test.csr \"System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=B03F5F7F11D50A3A\"");
            Console.WriteLine();
            Console.WriteLine("Output:");
            Console.WriteLine("   Compiled executable named as specified in the source file");
        }
    }
}
