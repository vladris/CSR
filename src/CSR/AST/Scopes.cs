using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace CSR.AST
{
    /// <summary>
    /// Base Scope class from which all other scopes ineherit
    /// </summary>
    abstract class Scope
    {
        // Parent symbol table
        public Scope parentScope;

        /// <summary>
        /// Retrieves the type of a variable
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <returns></returns>
        public abstract BaseType GetVariable(string referenceExpr);

        /// <summary>
        /// Retrieves the signature of a function
        /// </summary>
        /// <param name="callExpr">CallExpression for the function</param>
        /// <returns></returns>
        public abstract Signature GetFunction(CallExpression callExpr);

        /// <summary>
        /// Emits a reference to a variable given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public abstract void EmitVariableReference(string referenceExpr, ILGenerator ilGen);

        /// <summary>
        /// Retrieves metadata information for a function
        /// </summary>
        /// <param name="callExpr">CallExpression for the function</param>
        /// <returns></returns>
        public abstract MethodInfo GetMethodInfo(CallExpression callExpr);

        /// <summary>
        /// Emits a variable assignement given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public abstract void EmitVariableAssignement(string referenceExpr, ILGenerator ilGen);
    }

    /// <summary>
    /// GlobalScope represents the global scope while compiling. The global scope looks up symbols that
    /// are not declared in the source code but in referenced assemblies. This is the topmost scope.
    /// </summary>
    class GlobalScope : Scope
    {
        // Lookups are used to quickly retrieve MethodInfo and Signature objects for functions
        // that were found before
        private Dictionary<string, MethodInfo> methodInfoLookup;
        private Dictionary<string, Signature> signatureLookup;

        // Lookups are used to quickly retrieve FieldInfo and BaseType objects for variables
        // that were found before
        private Dictionary<string, FieldInfo> fieldInfoLookup;
        private Dictionary<string, BaseType> referenceLookup;

        // List of referenced assemblies
        private List<Assembly> references;

        /// <summary>
        /// Creates a new instance of GlobalScope
        /// </summary>
        /// <param name="references">List of assembly references</param>
        public GlobalScope(List<string> references)
        {
            // Initialize private members
            this.references = new List<Assembly>();
            this.methodInfoLookup = new Dictionary<string, MethodInfo>();
            this.signatureLookup = new Dictionary<string, Signature>();
            this.fieldInfoLookup = new Dictionary<string, FieldInfo>();
            this.referenceLookup = new Dictionary<string, BaseType>();

            // Load assemblies
            foreach (string file in references)
            {
                try
                {
                    // Load assembly given its strong name
                    this.references.Add(Assembly.Load(file));
                }
                catch
                {
                    // Issue error but continue compiling - assembly might not be needed and if it is, other
                    // errors will appear when external variables and functions are referenced
                    Console.WriteLine("Unable to load referenced assembly '{0}'", file);
                }
            }
        }

        /// <summary>
        /// Retrieves the type of a variable
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <returns></returns>
        public override BaseType GetVariable(string referenceExpr)
        {
            // If variable was already found, return it
            if (referenceLookup.ContainsKey(referenceExpr))
            {
                return referenceLookup[referenceExpr];
            }

            // Split reference FQN into type name and field name
            string typeName = referenceExpr.Substring(0, referenceExpr.LastIndexOf("."));
            string fieldName = referenceExpr.Substring(referenceExpr.LastIndexOf(".") + 1);

            // For each assembly reference
            foreach (Assembly asm in references)
            {
                // Get type (from FQN type)
                Type type = Type.GetType(typeName);

                // If type is not found step to next assembly
                if (type == null)
                {
                    continue;
                }

                // Set result
                FieldInfo result = type.GetField(fieldName);

                // Check if field is public and static (cannot reference instance fields)
                if (((result.Attributes & FieldAttributes.Static) == FieldAttributes.Static) &&
                    ((result.Attributes & FieldAttributes.Public) == FieldAttributes.Public))
                {
                    // Add to dictionaries
                    referenceLookup.Add(referenceExpr, BaseType.FromType(result.GetType()));
                    fieldInfoLookup.Add(referenceExpr, result);

                    return referenceLookup[referenceExpr];
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves an external function signature from a given call expression
        /// </summary>
        /// <param name="callExpr">CallExpression object</param>
        /// <returns>Closest matching signature or null if no matching function is found</returns>
        public override Signature GetFunction(CallExpression callExpr)
        {
            // Convert call expression to signature
            Signature sig = new Signature(callExpr);

            // Candidates to best match
            List<Signature> candidates = new List<Signature>();
            List<MethodInfo> candidatesMethodInfo = new List<MethodInfo>();

            // If signature was already found, return it
            if (signatureLookup.ContainsKey(sig.ToString()))
            {
                return signatureLookup[sig.ToString()];
            }

            // If we got to global scope and function doesn't have an FQN,
            // it means the function doesn't exist
            if (!sig.name.Contains("."))
            {
                return null;
            }
                
            // Split function FQN into type name and function name
            string typeName = sig.name.Substring(0, sig.name.LastIndexOf("."));
            string methodName = sig.name.Substring(sig.name.LastIndexOf(".") + 1);

            // For each assembly reference
            foreach (Assembly asm in references)
            {
                Type type = null;

                // Get type (from FQN type)
                foreach (Type t in asm.GetTypes())
                {
                    if (t.FullName == typeName)
                    {
                        type = t;
                    }
                }

                // If type is not found step to next assembly
                if (type == null)
                {
                    continue;
                }

                // Retrieve function list of the given type
                foreach (MethodInfo mi in type.GetMethods())
                {
                    // If name differs, step to next function
                    if (mi.Name != methodName)
                    {
                        continue;
                    }

                    // Create Signature object to test against
                    Signature testSig = new Signature(mi);

                    // Check if there is an exact match
                    if (sig.IsExactMatch(testSig))
                    {
                        // Add to dictionary
                        signatureLookup.Add(testSig.ToString(), testSig);
                        methodInfoLookup.Add(testSig.ToString(), mi);

                        return testSig;
                    }
                    
                    // If signatures are incompatible, step to next function
                    if (!sig.IsCompatible(testSig))
                    {
                        continue;
                    }

                    bool add = false, brk = false;

                    // Check current signature against each other candidate
                    foreach (Signature candSig in candidates)
                    {
                        // Pick best signature from current signature and candidate
                        switch (sig.BestSignature(candSig, testSig))
                        {
                            // If neither is better, mark add to add this function to the list of candidates
                            case Match.Ambiguos:
                                add = true;
                                break;
                            // If this is better than the candidate
                            case Match.SecondBest:
                                // Remove candidate and mark add to add this function to the list
                                candidatesMethodInfo.RemoveAt(candidates.IndexOf(candSig));
                                candidates.Remove(candSig);
                                add = true;
                                break;
                            // If the candidate is better than this function there is no need to evaluate
                            // against other candidates - there is a better match than this one already found
                            case Match.FirstBest:
                                brk = true;
                                break;
                        }

                        // Stop evaluation if brk is true
                        if (brk)
                        {
                            break;
                        }
                    }

                    // Add function to the list of candidates if add is true
                    if (add)
                    {
                        candidates.Add(testSig);
                        candidatesMethodInfo.Add(mi);
                    }
                }
            }

            // Check if candidate list is empty
            if (candidates.Count == 0)
            {
                // No match was found
                return null;
            }
            // Check if a single candidate is in the list
            else if (candidates.Count == 1)
            {
                // Add to dictionaries
                signatureLookup.Add(candidates[0].ToString(), candidates[0]);
                methodInfoLookup.Add(candidates[0].ToString(), candidatesMethodInfo[0]);

                return candidates[0];
            }
            // If more candidates are in the list
            else
            {
                // Ambiguos call - cannot determine which candidate is the best
                return null;
            }
        }

        /// <summary>
        /// Retrieves metadata information for a function
        /// </summary>
        /// <param name="callExpr">CallExpression for the function</param>
        /// <returns></returns>
        public override MethodInfo GetMethodInfo(CallExpression callExpr)
        {
            // Since signatures are always searched during the evaluation step,
            // a MethodInfo must exist in the dictionary
            return methodInfoLookup[new Signature(callExpr).ToString()];
        }

        /// <summary>
        /// Emits a reference to a variable given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public override void EmitVariableReference(string referenceExpr, ILGenerator ilGen)
        {
            // Emit a load static field instruction for the static field
            ilGen.Emit(OpCodes.Ldsfld, fieldInfoLookup[referenceExpr]);
        }

        /// <summary>
        /// Emits a variable assignement given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public override void EmitVariableAssignement(string referenceExpr, ILGenerator ilGen)
        {
            // Get FieldInfo object from dictionary
            FieldInfo fi = fieldInfoLookup[referenceExpr];

            // Emit a store to static field instruction for the static field
            ilGen.Emit(OpCodes.Stsfld, fi);
        }
    }

    /// <summary>
    /// ProgramScope represents the scope of the program which stores top level symbols and program name
    /// </summary>
    class ProgramScope : Scope
    {
        // The name of the executable
        public string name;

        // List of variable declarations
        public List<VariableDeclaration> variables;

        // List of function declarations
        public List<FunctionDeclaration> functions;

        // Entry point function
        public FunctionDeclaration entryPoint;

        /// <summary>
        /// Creates a new ProgramScope
        /// </summary>
        /// <param name="parentScope">Parent scope (GlobalScope object should be used)</param>
        public ProgramScope(Scope parentScope)
        {
            // Initialize private members
            functions = new List<FunctionDeclaration>();
            variables = new List<VariableDeclaration>();

            this.parentScope = parentScope;
        }

        /// <summary>
        /// Adds a function declaration
        /// </summary>
        /// <param name="decl">FunctionDeclaration object</param>
        public void AddFunction(FunctionDeclaration decl)
        {
            // Check if a function with the same exact signature was already declared
            foreach (FunctionDeclaration func in functions)
            {
                // Check if functions have the same name
                if (func.name == decl.name)
                {
                    // Check if functions have the same signature
                    if (func.ToSignature().IsExactMatch(decl.ToSignature()))
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(decl.t.line, decl.t.col, string.Format("Function '{0}' already declared", decl.name));
                    }
                }
            }

            // Add function to list
            functions.Add(decl);
        }

        /// <summary>
        /// Retrieves the type of a variable
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <returns></returns>
        public override BaseType GetVariable(string referenceExpr)
        {
            // Search the list of variables
            foreach (VariableDeclaration decl in variables)
            {
                if (decl.name == referenceExpr)
                {
                    return decl.type;
                }
            }

            // If no match is found, delegate request to parent scope
            return parentScope.GetVariable(referenceExpr);
        }

        /// <summary>
        /// Retrieves a function signature from a given call expression
        /// </summary>
        /// <param name="callExpr">CallExpression object</param>
        /// <returns></returns>
        public override Signature GetFunction(CallExpression callExpr)
        {
            // Convert call expression to signature
            Signature sig = new Signature(callExpr);

            // Candidates to best match
            List<Signature> candidates = new List<Signature>();

            // For each function in function list
            foreach (FunctionDeclaration decl in functions)
            {
                // Create signature
                Signature testSig = decl.ToSignature();

                // If name is different, continue to next function
                if (sig.name != testSig.name)
                {
                    continue;
                }

                // If an exact match is found, return it
                if (sig.IsExactMatch(testSig))
                {
                    return testSig;
                }

                // If signatures are incompatible, continue to next function
                if (!sig.IsCompatible(testSig))
                {
                    continue;
                }

                bool add = true, brk = false;

                // Check current signature against each other candidate
                foreach (Signature candSig in candidates)
                {
                    // Pick best signature from current signature and candidate
                    switch (sig.BestSignature(candSig, testSig))
                    {
                        // If neither is better, step to next function
                        case Match.Ambiguos:
                            break;
                        // If current signature is better, remove candidate from list
                        case Match.SecondBest:
                            candidates.Remove(candSig);
                            break;
                        // If candidate is better, drop this function 
                        case Match.FirstBest:
                            add = false;
                            brk = true;
                            break;
                    }

                    // If brk is true, stop evaluating
                    if (brk)
                    {
                        break;
                    }
                }

                // If add is true, add this function to the list of candidates
                if (add)
                {
                    candidates.Add(testSig);
                }
            }

            // Check if candidate list is empty
            if (candidates.Count == 0)
            {
                // No match was found, delegate request to parent scope
                return parentScope.GetFunction(callExpr);
            }
            // Check if a single match was found
            else if (candidates.Count == 1)
            {
                // Return match
                return candidates[0];
            }
            // More than one match found
            else
            {
                // Ambiguos function call
                return null;
            }
        }

        /// <summary>
        /// Emits a reference to a variable given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public override void EmitVariableReference(string referenceExpr, ILGenerator ilGen)
        {
            // Lookup variable in variable list
            foreach (VariableDeclaration decl in variables)
            {
                if (decl.name == referenceExpr)
                {
                    // Emit load static field instruction
                    ilGen.Emit(OpCodes.Ldsfld, decl.fb);
                    return;
                }
            }

            // If no variable is found, delegate request to parent scope
            parentScope.EmitVariableReference(referenceExpr, ilGen);
        }

        /// <summary>
        /// Retrieves a function MethodInfo from a given call expression
        /// </summary>
        /// <param name="callExpr">CallExpression object</param>
        /// <returns>MethodInfo of closest matching function or null if no matching function
        /// is found</returns>
        public override MethodInfo GetMethodInfo(CallExpression callExpr)
        {
            // Convert call expression to signature
            Signature sig = new Signature(callExpr);

            // Candidates to best match
            List<Signature> candidates = new List<Signature>();
            List<MethodInfo> candidatesMethodInfo = new List<MethodInfo>();

            // For each function in function list
            foreach (FunctionDeclaration decl in functions)
            {
                // Create signature
                Signature testSig = decl.ToSignature();

                // If name is different, continue to next function
                if (sig.name != testSig.name)
                {
                    continue;
                }

                // If an exact match is found, return it
                if (sig.IsExactMatch(testSig))
                {
                    return decl.mb;
                }

                // If signatures are incompatible, continue to next function
                if (!sig.IsCompatible(testSig))
                {
                    continue;
                }

                bool add = false, brk = false;

                // Check current signature against each other candidate
                foreach (Signature candSig in candidates)
                {
                    // Pick best signature from current signature and candidate
                    switch (sig.BestSignature(candSig, testSig))
                    {
                        // If neither is better, mark add to add this function to the list of candidates
                        case Match.Ambiguos:
                            add = true;
                            break;
                        // If this is better, remove the candidate from the list and mark add
                        case Match.SecondBest:
                            candidatesMethodInfo.RemoveAt(candidates.IndexOf(candSig));
                            candidates.Remove(candSig);
                            add = true;
                            break;
                        // If candidate is better, stop evaluating
                        case Match.FirstBest:
                            brk = true;
                            break;
                    }

                    // If brk is true, stop evaluating
                    if (brk)
                    {
                        break;
                    }
                }

                // If add is true, add this function to the candidate list
                if (add)
                {
                    candidates.Add(testSig);
                }
            }

            // Check if candidate list is empty
            if (candidates.Count == 0)
            {
                // If no match was found, delegate request to parent scope
                return parentScope.GetMethodInfo(callExpr);
            }
            // Check if a single match was found
            else if (candidates.Count == 1)
            {
                // Return match
                return candidatesMethodInfo[0];
            }
            // More than one match - ambiguos 
            else
            {
                // Cannot determine which function to call
                return null;
            }
        }

        /// <summary>
        /// Appends a list of variable declarations to the variable list
        /// </summary>
        /// <param name="decls">Variable declaration list</param>
        public void AddVariables(List<VariableDeclaration> decls)
        {
            variables.AddRange(decls);

            // Check if the same identifier appears twice in the variable declarations
            for (int i = 0; i < variables.Count - 1; i++)
            {
                for (int j = i + 1; j < variables.Count; j++)
                {
                    if (variables[i].name == variables[j].name)
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(variables[j].t.line, variables[j].t.col, string.Format("Identifier redeclared '{0}'", variables[j].name));
                    }
                }
            }
        }

        #region Evaluation

        /// <summary>
        /// Recursively evaluates the AST
        /// </summary>
        public void Evaluate()
        {
            // Add the entry point to the function declaration list
            functions.Add(entryPoint);

            // Evaluate each function declaration
            foreach (FunctionDeclaration decl in functions)
            {
                decl.Evaluate();
            }
        }

        #endregion

        #region Reflection

        // Reflection.Emit objectss
        private AssemblyName an;
        private AssemblyBuilder ab;
        private ModuleBuilder mb;
        private TypeBuilder tb;

        /// <summary>
        /// Emits all metadata
        /// </summary>
        public void EmitDeclaration()
        {
            // Create a new assembly and module
            an = new AssemblyName(name);
            ab = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Save);
            mb = ab.DefineDynamicModule(name, name + ".exe");

            // Emit metadata for each function declaration
            foreach (FunctionDeclaration decl in functions)
            {
                decl.EmitDeclaration(mb);
            }

            // Type is used to store global variables
            tb = mb.DefineType("Data", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            foreach (VariableDeclaration decl in variables)
            {
                decl.EmitDeclaration(tb);
            }

            // Creates a private, static constructor to instantiate any arrays
            ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Private | MethodAttributes.Static, CallingConventions.Standard, null);

            // Creates the IL Generator object for the constructor
            ILGenerator ilGen = cb.GetILGenerator();

            // Step through the variable list
            for (int i = 0; i < variables.Count; i++)
            {
                ArrayType arrType = variables[i].type as ArrayType;

                // If an array is found
                if (arrType != null)
                {
                    // Instantiate array
                    arrType.EmitInstance(ilGen);
                    ilGen.Emit(OpCodes.Stsfld, variables[i].fb);
                }
            }

            // Return
            ilGen.Emit(OpCodes.Ret);

            // Create Data class
            tb.CreateType();
        }

        /// <summary>
        /// Emits all code
        /// </summary>
        public void EmitCode()
        {  
            // EmitInstance code for each function declaration
            foreach (FunctionDeclaration decl in functions)
            {
                decl.EmitBody();
            }

            // Set entry point and finalize
            ab.SetEntryPoint(entryPoint.GetMethodInfo());
            mb.CreateGlobalFunctions();

            // Save assembly
            ab.Save(name + ".exe");
        }

        /// <summary>
        /// Emits a variable assignement given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public override void EmitVariableAssignement(string referenceExpr, ILGenerator ilGen)
        {
            // Step through variable list
            for (int i = 0; i < variables.Count; i++)
            {
                // If variable is found
                if (variables[i].name == referenceExpr)
                {
                    // Emit assignement
                    ilGen.Emit(OpCodes.Stsfld, variables[i].fb);
                    return;
                }
            }

            // If variable was not found, delegate request to parent scope
            parentScope.EmitVariableAssignement(referenceExpr, ilGen);
        }

        #endregion
    }

    /// <summary>
    /// LocalScope represents the scope of a function which stores its variables and arguments
    /// </summary>
    class LocalScope : Scope
    {
        // List of variable declarations and arguments
        public List<VariableDeclaration> variables;
        public List<VariableDeclaration> arguments;

        // Function return type
        public BaseType returnType;

        /// <summary>
        /// Creates a new LocalScope given a parent scope
        /// </summary>
        /// <param name="parentScope">Parent scope</param>
        public LocalScope(Scope parentScope)
        {
            // Initialize private members
            variables = new List<VariableDeclaration>();
            arguments = new List<VariableDeclaration>();
            
            this.parentScope = parentScope;
        }

        /// <summary>
        /// Adds a variable to the variable list
        /// </summary>
        /// <param name="decl"></param>
        public void AddVariable(VariableDeclaration decl)
        {
            // Check if the same identifier was already declared as a variable
            foreach (VariableDeclaration var in variables)
            {
                if (decl.name == var.name)
                {
                    // Issue error
                    Compiler.Compiler.errors.SemErr(decl.t.line, decl.t.col, string.Format("Identifier redeclared '{0}'", decl.name));
                }
            }
            variables.Add(decl);
        }

        /// <summary>
        /// Adds an argument to the argument list
        /// </summary>
        /// <param name="decl"></param>
        public void AddArgument(VariableDeclaration decl)
        {
            // Check if the same identifier was already declared as an argument
            foreach (VariableDeclaration arg in arguments)
            {
                if (decl.name == arg.name)
                {
                    // Issue error
                    Compiler.Compiler.errors.SemErr(decl.t.line, decl.t.col, string.Format("Identifier redeclared '{0}'", decl.name));
                }
            }

            arguments.Add(decl);
        }

        /// <summary>
        /// Appends a list of variables to the variable list
        /// </summary>
        /// <param name="decls"></param>
        public void AddVariables(List<VariableDeclaration> decls)
        {
            variables.AddRange(decls);

            // Check if the same identifier appears twice in the variable declarations
            for (int i = 0; i < variables.Count - 1; i++)
            {
                for (int j = i + 1; j < variables.Count; j++)
                {
                    if (variables[i].name == variables[j].name)
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(variables[j].t.line, variables[j].t.col, string.Format("Identifier redeclared '{0}'", variables[j].name));
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the type of a variable
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <returns></returns>
        public override BaseType GetVariable(string referenceExpr)
        {
            // Search for variable in variable list
            BaseType result = GetVariable(referenceExpr, variables);

            // Check if no variable was found
            if (result == null)
            {
                // Search for variable in argument list
                result = GetVariable(referenceExpr, arguments);

                // Check if no argument was found
                if (result == null)
                {
                    // Delegate request to parent scope
                    return parentScope.GetVariable(referenceExpr);
                }
            }

            return result;                
        }

        /// <summary>
        /// Looks up a variable in a given list of variables
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="declList">List of variables</param>
        /// <returns></returns>
        private BaseType GetVariable(string referenceExpr, List<VariableDeclaration> declList)
        {
            // Step through variable list
            foreach (VariableDeclaration decl in declList)
            {
                if (decl.name == referenceExpr)
                {
                    // Return match
                    return decl.type;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves a function signature from a given call expression
        /// </summary>
        /// <param name="callExpr">CallExpression object</param>
        /// <returns></returns>
        public override Signature GetFunction(CallExpression callExpr)
        {
            // Delegate request to parent scope since functions cannot be declared inside other functions
            return parentScope.GetFunction(callExpr);
        }

        /// <summary>
        /// Emits a reference to a variable given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public override void EmitVariableReference(string referenceExpr, ILGenerator ilGen)
        {
            // Step through variable list
            for (int i = 0; i < variables.Count; i++)
            {
                if (variables[i].name == referenceExpr)
                {
                    // If variable is found, load local variable and return
                    ilGen.Emit(OpCodes.Ldloc, (short)i);
                    return;
                }
            }

            // Step through argument list
            for (int i = 0; i < arguments.Count; i++)
            {
                if (arguments[i].name == referenceExpr)
                {
                    // If argument is found, load argument and return
                    ilGen.Emit(OpCodes.Ldarg, (short)i);
                    return;
                }
            }

            // If no match was found, delegate request to parent scope
            parentScope.EmitVariableReference(referenceExpr, ilGen);
        }

        /// <summary>
        /// Emits a variable assignement given its name
        /// </summary>
        /// <param name="referenceExpr">Variable name</param>
        /// <param name="ilGen">IL Generator object</param>
        public override void EmitVariableAssignement(string referenceExpr, ILGenerator ilGen)
        {
            // Step through variable list
            for (int i = 0; i < variables.Count; i++)
            {
                if (variables[i].name == referenceExpr)
                {
                    // If variable is found, emit store to locale and return
                    ilGen.Emit(OpCodes.Stloc, (short)i);
                    return;
                }
            }

            // If no match was found, delegate request to parent scope
            parentScope.EmitVariableAssignement(referenceExpr, ilGen);
        }

        /// <summary>
        /// Retrieves metadata information for a function
        /// </summary>
        /// <param name="callExpr">CallExpression for the function</param>
        /// <returns></returns>
        public override MethodInfo GetMethodInfo(CallExpression callExpr)
        {
            // Delegate request to parent scope since functions cannot be declared inside other functions
            return parentScope.GetMethodInfo(callExpr);
        }

        /// <summary>
        /// Emit local variable declaration and array instantiation code
        /// </summary>
        /// <param name="ilGen">IL Generator object</param>
        public void EmitCode(ILGenerator ilGen)
        {
            // For each variable in variable list
            foreach (VariableDeclaration decl in variables)
            {
                // Declare local variable
                ilGen.DeclareLocal(decl.type.ToCLRType());
            }

            // Step through the variable list
            for (int i = 0; i < variables.Count; i++)
            {
                ArrayType arrType = variables[i].type as ArrayType;

                // If an array variable is found
                if (arrType != null)
                {
                    // Instantiate array
                    arrType.EmitInstance(ilGen);
                    ilGen.Emit(OpCodes.Stloc, (short)i);
                }
            }
        }
    }
}
