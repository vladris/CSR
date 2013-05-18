using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using CSR.Parser;

namespace CSR.AST
{
    /// <summary>
    /// Abstract Declaration class from which all other declarations inherit
    /// </summary>
    abstract class Declaration
    {
        // Token is remembered for error reporting
        public Token t;

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        public abstract void Evaluate();
    }

    /// <summary>
    /// FunctionDeclaration represents a function declaration with name, arguments, return type and body
    /// </summary>
    class FunctionDeclaration : Declaration
    {
        // Name, return type, arguments and body
        public string name;
        public BaseType returnType;
        public List<BaseType> arguments;
        public BlockStatement body;

        // Scope associated with this function
        public LocalScope localScope;

        /// <summary>
        /// Creates a new FunctionDeclaration given a parent scope
        /// </summary>
        /// <param name="parentScope">Parent scope of this function</param>
        private FunctionDeclaration(Scope parentScope)
        {
            // Initialize private members
            localScope = new LocalScope(parentScope);
            arguments = new List<BaseType>();
            returnType = PrimitiveType.VOID;
        }

        /// <summary>
        /// Creates a new FunctionDeclaration given a token and a parent scope
        /// </summary>
        /// <param name="t">Function declaration token</param>
        /// <param name="parentScope">Parent scope of this function</param>
        public FunctionDeclaration(Token t, Scope parentScope)
            : this(parentScope)
        {
            // Initialize private members
            this.t = t;
            this.name = t.val;
        }

        /// <summary>
        /// Creates a new FunctionDeclaration given a function name and a parent scope
        /// </summary>
        /// <param name="name">Function name</param>
        /// <param name="parentScope">Parent scope of this function</param>
        public FunctionDeclaration(string name, Scope parentScope)
            : this(parentScope)
        {
            this.name = name;
        }

        /// <summary>
        /// Adds an argument to the function's argument list
        /// </summary>
        /// <param name="t">Argument token</param>
        /// <param name="type">Argument type</param>
        public void AddArgument(Token t, BaseType type)
        {
            // Add argument to the local symbol table as a variable declaration
            localScope.AddArgument(new VariableDeclaration(t, type));
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        public override void Evaluate()
        {
            // Set return type to void if it is not set
            if (returnType == null)            
            {
                returnType = PrimitiveType.VOID;
            }

            localScope.returnType = returnType;

            // Evaluate function body
            body.Evaluate(localScope);

            // Check if body doesn't return
            // Functions which return void don't have to contain an explicit return statement
            if (!body.returns)
            {
                // Check if return type is void
                if ((returnType is PrimitiveType) && (returnType as PrimitiveType).type == Primitive.Void)
                {
                    // Insert return statement
                    body.AddStatement(new ReturnStatement());
                }
                else
                {
                    // If return type is not void, function must return - issue error
                    Compiler.Compiler.errors.SemErr(t.line, t.col, "not all code paths return a value"); 
                }
            }
        }

        // Used for code generation
        public MethodBuilder mb;

        /// <summary>
        /// Emits metadata declaration for this function given a ModuleBuilder object
        /// </summary>
        /// <param name="mb">ModuleBuilder object</param>
        public void EmitDeclaration(ModuleBuilder mb)
        {
            // Define global public static method
            this.mb = mb.DefineGlobalMethod(name, MethodAttributes.Public | MethodAttributes.Static, Type.GetType("System.Void"), null);
            
            // Setup arguments
            Type[] types = new Type[localScope.arguments.Count];

            for (int i = 0; i < localScope.arguments.Count; i++)
            {
                types[i] = localScope.arguments[i].type.ToCLRType();
            }

            // Set method arguments
            this.mb.SetParameters(types);

            // Set return type
            this.mb.SetReturnType(returnType.ToCLRType());
        }

        /// <summary>
        /// Emits the body of the function
        /// </summary>
        public void EmitBody()
        {
            // Retrieve IL Generator object
            ILGenerator ilGen = mb.GetILGenerator();

            // Emit local variable declarations
            localScope.EmitCode(ilGen);    

            // Emit body code
            body.EmitCode(ilGen, localScope);
        }

        /// <summary>
        /// Gets the MethodInfo object associated with this function
        /// </summary>
        /// <returns></returns>
        public MethodInfo GetMethodInfo()
        {
            // Return the MethodBuilder (which inherits from MethodInfo)
            return mb;
        }

        /// <summary>
        /// Converts this function to a signature
        /// </summary>
        /// <returns></returns>
        public Signature ToSignature()
        {
            // Create and setup singature
            Signature result = new Signature();
            result.name = this.name;
            result.returnType = this.returnType;
            result.arguments = new List<BaseType>();
            
            // Add arguments to signature
            foreach (VariableDeclaration decl in localScope.arguments)
            {
                result.arguments.Add(decl.type);
            }

            return result;
        }
    }

    /// <summary>
    /// VariableDeclaration represents a global variable declaration with name and type
    /// </summary>
    class VariableDeclaration : Declaration
    {
        // Name and type
        public string name;
        public BaseType type;

        /// <summary>
        /// Creates a new VariableDeclaration given a token and a type
        /// </summary>
        /// <param name="t">Declaration token</param>
        /// <param name="type">Variable type</param>
        public VariableDeclaration(Token t, BaseType type)
        {
            // Set members
            this.t = t;
            this.name = t.val;
            this.type = type;
        }

        /// <summary>
        /// Evaluates this node
        /// </summary>
        public override void Evaluate()
        {
            // No evaluation needed
        }

        // Used for code generation
        public FieldBuilder fb;

        /// <summary>
        /// Emits metadata declaration for this variable given a TypeBuilder object
        /// </summary>
        /// <param name="tb">TypeBuilder object</param>
        public void EmitDeclaration(TypeBuilder tb)
        {
            // Create public static field
            fb = tb.DefineField(name, type.ToCLRType(), FieldAttributes.Public | FieldAttributes.Static);
        }
    }
}
