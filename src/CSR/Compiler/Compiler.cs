using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using CSR.AST;
using CSR.Parser; 

namespace CSR.Compiler
{
    /// <summary>
    /// Compiler class wrapping all other necessary components
    /// </summary>
    class Compiler
    {
        // List of assembly references
        public List<string> references;

        // Source file
        public string file;

        // Error list
        public static Errors errors;

        // Global and program scopes
        public GlobalScope globalScope;
        public ProgramScope programScope;

        // Static scope needed by the parser
        public static ProgramScope staticScope;

        /// <summary>
        /// Creates a new compiler object given a source file
        /// </summary>
        /// <param name="file">Source file name</param>
        public Compiler(string file)
        {
            // Initialize members
            references = new List<string>();
            this.file = file;

            errors = new Errors();
        }

        /// <summary>
        /// Adds an assembly reference
        /// </summary>
        /// <param name="reference"></param>
        public void AddReference(string reference)
        {
            references.Add(reference);
        }

        /// <summary>
        /// Compiles the source file
        /// </summary>
        public void Compile()
        {
            // Initialize scopes
            globalScope = new GlobalScope(references);
            programScope = new ProgramScope(globalScope);

            staticScope = programScope;

            errors.count = 0;

            // Create scanner and parser
            Scanner scanner = new Scanner(file);
            CSR.Parser.Parser parser = new CSR.Parser.Parser(scanner);
            parser.Parse();

            // Check for any parsing errors
            if (parser.errors.count == 0)
            {
                // Evaluate the AST
                staticScope.Evaluate();

                // Check for any semantic errors
                if (errors.count == 0)
                {
                    // Emit metadata
                    staticScope.EmitDeclaration();
                    // Emit IL code
                    staticScope.EmitCode();

                    Console.WriteLine("Done        ");
                }
                else
                {
                    // Abort if semantic errors were encountered
                    errors.Warning("Compilation aborted");
                }
            }
            else
            {
                // Abort if parsing errors were encountered
                errors.Warning("Compilation aborted");
            }
        }
    }
}
