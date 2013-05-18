using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using CSR.Parser;

namespace CSR.AST
{
    /// <summary>
    /// Abstract Expression class from which all other expressions inherit
    /// </summary>
    abstract class Expression
    {
        // Token is remembered for error reporting
        public Token t;

        // Return type (since all expressions, by definition, return a value)
        public BaseType returnType;

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public abstract Expression Evaluate(Scope scope);
        
        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public abstract void EmitCode(ILGenerator ilGen, Scope scope);
    }

    /// <summary>
    /// ConstantExpression represents a literal value in the source code 
    /// </summary>
    class ConstantExpression : Expression
    {
        // Value object (can be of any supported primitive type)
        public object value;

        /// <summary>
        /// Creates a new ConstantExpression given a primitive type and a token
        /// </summary>
        /// <param name="type">Constant type</param>
        /// <param name="t">Token containing value</param>
        public ConstantExpression(Primitive type, Token t)
        {
            // Set return type and token
            this.returnType = new PrimitiveType(type);
            this.t = t;

            // Processing is selected based on type
            switch (type)
            {
                // Process string
                case Primitive.String:
                    // Format string replacing escape characters
                    value = FormatString(t.val);
                    break;

                // Process integer
                case Primitive.Int:
                    try
                    {
                        // Attempt parsing the value of the token
                        value = Int32.Parse(t.val, NumberFormatInfo.InvariantInfo);
                    }
                    catch
                    {
                        value = 0;

                        // Invalid integer literal
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Invalid integer literal");
                    }
                    break;

                // Process double
                case Primitive.Double:
                    try
                    {
                        // Attempt parsing the value of the token
                        value = Double.Parse(t.val, NumberFormatInfo.InvariantInfo);
                    }
                    catch
                    {
                        value = 0;

                        // Invalid double literal
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Invalid double literal");
                    }
                    break;

                // Process boolean
                case Primitive.Bool:
                    try
                    {
                        // Attempt parsing the value of the token
                        value = Boolean.Parse(t.val);
                    }
                    catch
                    {
                        // Invalid boolean literal
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Invalid boolean literal");
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Creates a new ConstantExpression given a primitive type and a string
        /// </summary>
        /// <param name="type">Constant type</param>
        /// <param name="val">Value of constant as string</param>
        public ConstantExpression(Primitive type, string val)
        {
            // Since this is used internally by the tree evaluator and optimizer, literals are guaranteed to be valid.
            // No token info is saved and no error handling is needed
            this.returnType = new PrimitiveType(type);

            // Processing is selected based on type
            switch (type)
            {
                // Process string
                case Primitive.String:
                    value = FormatString(val);
                    break;
                // Process integer
                case Primitive.Int:
                    value = Int32.Parse(val, NumberFormatInfo.InvariantInfo);
                    break;
                // Process double
                case Primitive.Double:
                    value = Double.Parse(val, NumberFormatInfo.InvariantInfo);
                    break;
                // Process boolean
                case Primitive.Bool:
                    value = Boolean.Parse(val);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Creates a new ConstantExpression given a primitive type and an object
        /// </summary>
        /// <param name="type">Constant type</param>
        /// <param name="obj">Value as an object</param>
        public ConstantExpression(Primitive type, object obj)
            : this(type, obj.ToString())
        {
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Return type is set in constructor

            // No changes needed
            return this;
        }

        /// <summary>
        /// Determins wheather this constant expression evaluates to a non-zero value
        /// </summary>
        /// <returns></returns>
        public bool IsTrue()
        {
            // Evaluation is done depending on type
            switch (((PrimitiveType)returnType).type)
            {
                case Primitive.Bool:
                    // For booleans, the object is just typecasted
                    return (bool)value;
                case Primitive.Double:
                    // For doubles, the object is typecasted and compared to 0
                    return (double)value != 0;
                case Primitive.Int:
                    // For integers, the object is typecasted and compared to 0
                    return (int)value != 0;
                case Primitive.String:
                    // For strings, object must not be null
                    return value != null;
                case Primitive.Unsupported:
                case Primitive.Void:
                    // If constant is void or type is unsupported, return false
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Code generation method is determined depending on type
            switch (((PrimitiveType)returnType).type)
            {
                // Emit code for boolean value
                case Primitive.Bool:
                    if ((bool)value)
                    {
                        // If boolean is true, load 1 onto the stack (the CLR doesn't have boolean types)
                        ilGen.Emit(OpCodes.Ldc_I4_1);
                    }
                    else
                    {
                        // If boolean is false, load 0 onto the stack
                        ilGen.Emit(OpCodes.Ldc_I4_0);
                    }
                    break;

                // Emit code for string
                case Primitive.String:
                    // Load string onto the stack
                    ilGen.Emit(OpCodes.Ldstr, (string)value);
                    break;

                // Emit code for integer value
                case Primitive.Int:
                    // Cast to int
                    int val = (int)value;

                    // Perform CLR dependent optimiztion - use short form int constant loading
                    switch (val)
                    {
                        case 0: 
                            ilGen.Emit(OpCodes.Ldc_I4_0);
                            break;
                        case 1: 
                            ilGen.Emit(OpCodes.Ldc_I4_1);
                            break;
                        case 2: 
                            ilGen.Emit(OpCodes.Ldc_I4_2);
                            break;
                        case 3: 
                            ilGen.Emit(OpCodes.Ldc_I4_3);
                            break;
                        case 4: 
                            ilGen.Emit(OpCodes.Ldc_I4_4);
                            break;
                        case 5: 
                            ilGen.Emit(OpCodes.Ldc_I4_5);
                            break;
                        case 6: 
                            ilGen.Emit(OpCodes.Ldc_I4_6);
                            break;
                        case 7: 
                            ilGen.Emit(OpCodes.Ldc_I4_7);
                            break;
                        case 8: 
                            ilGen.Emit(OpCodes.Ldc_I4_8);
                            break;
                        default:
                            // If constant is greater than 8, use normal load instruction
                            ilGen.Emit(OpCodes.Ldc_I4, (int)value);
                            break;
                    }

                    break;

                // Emit code for double
                case Primitive.Double:
                    // Load double value onto the stack
                    ilGen.Emit(OpCodes.Ldc_R8, (double)value);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Formats a string by removing first and last characters (") and replacing escape characters with theri respective
        /// values
        /// </summary>
        /// <param name="str">String to format</param>
        /// <returns></returns>
        private string FormatString(string str)
        {
            // Trim quotes
            string tmp = str.Substring(1, str.Length - 2);

            // Formatted string
            string formatted = "";
            int i = 0;

            // Format string replacing escape characters \\, \r, \n, \t and \" with their respective single character values
            while (i < tmp.Length)
            {
                // If current character is different than \, add it to the formatted string
                if (tmp[i] != '\\')
                {
                    formatted += tmp[i];
                    i++;
                }
                else
                {
                    i++;
                    // Process escaped characters
                    switch (tmp[i])
                    {
                        case '\\':
                            formatted += '\\';
                            break;
                        case 'r':
                            formatted += '\r';
                            break;
                        case 'n':
                            formatted += '\n';
                            break;
                        case 't':
                            formatted += '\t';
                            break;
                        case '"':
                            formatted += '"';
                            break;
                        default:
                            // Invalid escape character
                            Compiler.Compiler.errors.SemErr(t.line, t.col, string.Format("Unrecognized escape character '" + tmp[i] + "'"));
                            break;
                    }
                    i++;
                }
            }

            // Return formatted string
            return formatted;
        }
    }

    /// <summary>
    /// CallExpression represents a call to a method
    /// </summary>
    class CallExpression : Expression
    {
        // Name of method to call
        public string methodName;

        // Argument list composed by expressions
        public List<Expression> arguments;

        /// <summary>
        /// Creates a new CallExpression
        /// </summary>
        public CallExpression()
        {
            arguments = new List<Expression>();
        }

        /// <summary>
        /// Creates a new CallExpression given a method name and a token
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="t">Call token</param>
        public CallExpression(string methodName, Token t)
            : this()
        {
            // Set method name and token
            this.methodName = methodName;
            this.t = t;
        }

        /// <summary>
        /// Adds an agrument expression to the argument list
        /// </summary>
        /// <param name="expr">Argument expression</param>
        public void AddArgument(Expression expr)
        {
            arguments.Add(expr);
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Evaluate all arguments
            for (int i = 0; i < arguments.Count; i++)
            {
                arguments[i] = arguments[i].Evaluate(scope);
            }

            // Get signature from symbol table
            Signature sig = scope.GetFunction(this);

            // If no function is retrieved
            if (sig == null)
            {
                // Unknown function error
                Compiler.Compiler.errors.SemErr(t.line, t.col, "Unknown function or ambiguos call to " + methodName);
            }
            else
            {
                // Set return type
                this.returnType = sig.returnType;

                // Step through each argument
                for (int i = 0; i < sig.arguments.Count; i++)
                {
                    // If argument expression type is different than expected argument
                    // Arguments are guaranteed to be compatible since a matching signature was found in the symbol table
                    if (!arguments[i].returnType.Equals(sig.arguments[i]))
                    {
                        // Create implicit type cast to expected argument
                        Expression expr = arguments[i];
                        arguments[i] = new CastExpression(sig.arguments[i]);
                        (arguments[i] as CastExpression).operand = expr;
                    }
                }
            }             
            
            return this;
        }        

        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit code for all arguments
            foreach (Expression expr in arguments)
            {
                expr.EmitCode(ilGen, scope);
            }

            // Get metadata token for method from symbol table and emit call
            ilGen.Emit(OpCodes.Call, scope.GetMethodInfo(this));
        }
    }

    /// <summary>
    /// Unary operators
    /// </summary>
    enum UnaryOperator
    {
        UMinus, // Unary minus
        Not // Logical negation
    }

    /// <summary>
    /// UnaryExpression represents an expression with a single operand
    /// </summary>
    class UnaryExpression : Expression
    {
        // Operator and operand
        public UnaryOperator op;
        public Expression operand;

        /// <summary>
        /// Creates a new UnaryExpression given an operator
        /// </summary>
        /// <param name="op">Expression operator</param>
        public UnaryExpression(UnaryOperator op)
        {
            this.op = op;
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Evaluate operand expression
            operand = operand.Evaluate(scope);

            // Save operand return type
            PrimitiveType pType = (PrimitiveType)operand.returnType;

            if (pType == null)
            {
                return null;
            }

            // Processing is done depending on operator
            switch (op)
            {
                // Process unary minus
                case UnaryOperator.UMinus:
                    // If operand is integer or double
                    if (pType.type == Primitive.Int || pType.type == Primitive.Double)
                    {
                        // Set return type
                        this.returnType = new PrimitiveType(pType.type);

                        // If operand is constant perform folding
                        if (operand is ConstantExpression)
                        {
                            // Fold integer value
                            if (pType.type == Primitive.Int)
                            {
                                // Cast value to int, add unary minus and create new constant expression
                                return new ConstantExpression(Primitive.Int, (-(int)(operand as ConstantExpression).value).ToString());
                            }
                            else if (pType.type == Primitive.Double)
                            {
                                // Cast value to double, add unary minus and create new constant expression
                                return new ConstantExpression(Primitive.Double, (-(double)(operand as ConstantExpression).value).ToString());
                            }
                        }
                    }
                    else
                    {
                        // Unary minus cannot be applied on other types
                        this.returnType = PrimitiveType.UNSUPPORTED;

                        // Issue error
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Can only apply '-' to numeric types");
                        return this;
                    }
                    break;

                // Process logical negation
                case UnaryOperator.Not:
                    // If operand is boolean
                    if (pType.type == Primitive.Bool)
                    {
                        // Set return type
                        this.returnType = new PrimitiveType(pType.type);

                        // If oprand is constant perform folding
                        if (operand is ConstantExpression)
                        {
                            // Cast value to bool, negate and create new constant expression
                            return new ConstantExpression(Primitive.Bool, (bool)(operand as ConstantExpression).value ? "false" : "true");
                        }
                    }
                    else
                    {
                        // Logical negation cannot be applied on other types
                        this.returnType = PrimitiveType.UNSUPPORTED;

                        // Issue error
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Can only apply '!' to boolean types");
                        return this;
                    }
                    break;
            }

            return this;
        }

        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit code for operand
            operand.EmitCode(ilGen, scope);

            // Emitted code depends on operator
            switch (op)
            {
                // Emit code for unary minus
                case UnaryOperator.UMinus:
                    // Call neg
                    ilGen.Emit(OpCodes.Neg);
                    break;
                // Emit code for logical negation
                case UnaryOperator.Not:
                    // Logical negation is done by comparing value with 0 - opcode not creates a bitwise complement, not a
                    // negation
                    ilGen.Emit(OpCodes.Ldc_I4_0);
                    ilGen.Emit(OpCodes.Ceq);
                    break;
            }
        }
    }

    /// <summary>
    /// CastExpression represents a typecast
    /// </summary>
    class CastExpression : Expression
    {
        // Operand expression
        public Expression operand;

        /// <summary>
        /// Creates a new CastExpression given a type to cast to
        /// </summary>
        /// <param name="type">BaseType to cast to</param>
        public CastExpression(BaseType type)
        {
            // Set return type
            returnType = type;
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Evaluate operand
            operand = operand.Evaluate(scope);

            // If types are actually equal
            if (operand.returnType.Equals(this.returnType))
            {
                // Issue warning
                Compiler.Compiler.errors.Warning(t.line, t.col, "Typecast to the same type");

                // Drop the typecast expression
                return operand;
            }

            // If an implicit type cast exists, no need to evaluate further
            if (operand.returnType.IsCompatible(this.returnType))
            {
                if (returnType.Equals(PrimitiveType.DOUBLE))
                {
                    // If operand is constant, fold typecast
                    if (operand is ConstantExpression)
                    {
                        // Cast value to int, convert to double and create new constant expression
                        return new ConstantExpression(Primitive.Double, Convert.ToDouble((int)(operand as ConstantExpression).value));
                    }

                    return this;
                }
            }

            // Only explicit typecast implemented is from Double to Int
            if (operand.returnType.Equals(PrimitiveType.DOUBLE))
            {
                if (returnType.Equals(PrimitiveType.INT))
                {
                    // If operand is constant, fold typecast
                    if (operand is ConstantExpression)
                    {
                        // Cast value to double, convert to int and create new constant expression
                        return new ConstantExpression(Primitive.Int, Convert.ToInt32((double)(operand as ConstantExpression).value));
                    }

                    return this;
                }
            }

            // Execution shouldn't reach this line if cast is correct - issue error
            Compiler.Compiler.errors.SemErr(t.line, t.col, "Invalid typecast");
            return this;
        }

        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit code for operand
            operand.EmitCode(ilGen, scope);

            // Only typecasts available are between primitive types
            if (returnType is PrimitiveType)
            {
                // Code is emitted depending on cast type
                switch (((PrimitiveType)returnType).type)
                {
                    // Emit code for int cast
                    case Primitive.Int:
                        ilGen.Emit(OpCodes.Conv_I4);
                        break;
                    // Emit code for double cast
                    case Primitive.Double:
                        ilGen.Emit(OpCodes.Conv_R8);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// AssignableExpression represents an abstract class from which all assignable expression inherit, such as variable 
    /// references and array indexers
    /// </summary>
    abstract class AssignableExpression : Expression
    {
        // Primitive or array name - all assignable expressions have names
        public string name;

        /// <summary>
        /// Emits code for an assignement given the right-side expression of the assignement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        /// <param name="rightSide">Right-side expression</param>
        public abstract void EmitAssignement(ILGenerator ilGen, Scope scope, Expression rightSide);
    }

    /// <summary>
    /// IndexerExpression represents an indexed array position
    /// </summary>
    class IndexerExpression : AssignableExpression
    {
        // Expression that returns an array
        public Expression operand;

        // List of indexers for each dimension
        public List<Expression> indexers;

        /// <summary>
        /// Creates a new IndexerExpression given an array reference
        /// </summary>
        /// <param name="expr">Variable reference expression</param>
        public IndexerExpression(Expression expr)
        {
            // Initialize members
            operand = expr;
            t = expr.t;

            indexers = new List<Expression>();
        }

        /// <summary>
        /// Adds an indexer to the expression
        /// </summary>
        /// <param name="indexer">Indexer expression</param>
        public void AddIndexer(Expression indexer)
        {
            indexers.Add(indexer);
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Iterate through indexers
            for (int i = 0; i < indexers.Count; i++)
            {
                // Evaluate indexer
                indexers[i] = indexers[i].Evaluate(scope);

                // Check if indexer is of integer type
                if (!indexers[i].returnType.Equals(PrimitiveType.INT))
                {
                    // Ilegal indexer type - issue error
                    Compiler.Compiler.errors.SemErr(t.line, t.col, "Indexers must be of integer type");
                    return this;
                }
            }

            // Evaluate operand expression
            operand = operand.Evaluate(scope);

            if (operand is IndexerExpression)
            {
                Compiler.Compiler.errors.SemErr(operand.t.line, operand.t.col, "Cannot apply multiple indexers on array. Use [,] instead of [][]");
                return this;
            }

            // Try to cast operand return type to ArrayType
            ArrayType refType = operand.returnType as ArrayType;

            // Check if cast was successful
            if (refType == null)
            {
                // Indexers can only be applied to array types - issue error
                Compiler.Compiler.errors.SemErr(t.line, t.col, "Cannot apply indexers to non-array type");
                return this;
            }

            // Check if the dimension of the array is equal to the number of indexers
            if (refType.dimensions != indexers.Count)
            {
                // Cannot assign to arrays, only to values - issue error
                Compiler.Compiler.errors.SemErr(t.line, t.col, "Invalid number of indexers");
                return this;
            }

            // Set return type
            this.returnType = refType.type;

            return this;
        }

        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit code to index array
            EmitIndexers(ilGen, scope);

            // Call Get to retrieve value at indexed position
            ilGen.Emit(OpCodes.Call, ((ArrayType)operand.returnType).ToCLRType().GetMethod("Get"));
        }

        /// <summary>
        /// Emits code for assignement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        /// <param name="rightSide">Right-side expression</param>
        public override void EmitAssignement(ILGenerator ilGen, Scope scope, Expression rightSide)
        {
            // Emit code to index array
            EmitIndexers(ilGen, scope);

            // Emit code for right-side expression
            rightSide.EmitCode(ilGen, scope);

            // Call Set to store value at indexed position
            ilGen.Emit(OpCodes.Call, ((ArrayType)operand.returnType).ToCLRType().GetMethod("Set"));
        }

        /// <summary>
        /// Emits code for indexers
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        private void EmitIndexers(ILGenerator ilGen, Scope scope)
        {
            // Emit code for the variable reference
            operand.EmitCode(ilGen, scope);

            // Emit code for each indexer
            foreach (Expression indexer in indexers)
            {
                indexer.EmitCode(ilGen, scope);
            }
        }
    }

    /// <summary>
    /// Binary operators
    /// </summary>
    enum BinaryOperator
    {
        // Arithmetic operators
        Add = 1, // Addition
        Sub, // Subtraction
        Mul, // Multiplication
        Div, // Division
        Rem, // Modulo

        // Equality operators
        Eq, // Equals
        Neq, // Not equals
        Leq, // Less than or equals
        Geq, // Greater than or equals
        Lt, // Less than
        Gt, // Greater than

        // Logical operators
        And, // Logical and
        Or, // Logical or
        Xor // Logical exlusive or
    }

    /// <summary>
    /// BinaryExpression represents an expression with two operands
    /// </summary>
    class BinaryExpression : Expression
    {
        // Left and right operands and operator
        public Expression leftOperand;
        public Expression rightOperand;
        public BinaryOperator op;

        /// <summary>
        /// Creates a new BinaryExpression given a left operand
        /// </summary>
        /// <param name="leftOperand"></param>
        public BinaryExpression(Expression leftOperand)
        {
            this.leftOperand = leftOperand;
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Evaluate operands
            leftOperand = leftOperand.Evaluate(scope);
            rightOperand = rightOperand.Evaluate(scope);

            // Check if both operands are of the same type
            if (leftOperand.returnType.ToCLRType() != rightOperand.returnType.ToCLRType())
            {
                // If not, check if left operand can be implicitely typecasted to right operand type
                if (leftOperand.returnType.IsCompatible(rightOperand.returnType))
                {
                    // Create implicit cast
                    CastExpression implicitCast = new CastExpression(rightOperand.returnType);
                    implicitCast.operand = leftOperand;

                    // Evaluate typecast for folding
                    this.leftOperand = implicitCast.Evaluate(scope);

                    // Set return type
                    this.returnType = rightOperand.returnType;
                }
                // If not, check if right operand can be implicitely typecasted to left operand type
                else if (rightOperand.returnType.IsCompatible(leftOperand.returnType))
                {
                    // Create implicit cast
                    CastExpression implicitCast = new CastExpression(leftOperand.returnType);
                    implicitCast.operand = rightOperand;

                    // Evaluate for folding
                    this.rightOperand = implicitCast.Evaluate(scope);

                    // Set return type
                    this.returnType = leftOperand.returnType;
                }
                else
                {
                    // Types are incompatible - issue error
                    Compiler.Compiler.errors.SemErr(t.line, t.col, "Incompatible types");
                    this.returnType = PrimitiveType.UNSUPPORTED;
                    return this;
                }
            }
            // If operands are of the same type
            else
            {
                // Set return type
                this.returnType = leftOperand.returnType;
            }

            // If operand is not arithmetic, overwrite return type
            if ((int)op >= 6)
            {
                this.returnType = PrimitiveType.BOOL;
            }

            // Check if operator and operands are compatible
            switch (op)
            {
                // Addition is accepted for strings and numeric types
                case BinaryOperator.Add:
                    // If type is not string, check if it is numerical
                    if (!leftOperand.returnType.Equals(PrimitiveType.STRING))
                    {
                        goto case BinaryOperator.Leq;
                    }
                    break;
                // Other arithmetic operators and comparisons (except equals and not equals) are accepted for numeric types
                case BinaryOperator.Sub:
                case BinaryOperator.Mul:
                case BinaryOperator.Div:
                case BinaryOperator.Gt:
                case BinaryOperator.Lt:
                case BinaryOperator.Geq:
                case BinaryOperator.Leq:
                    // Check if type is not integer or double
                    if (!leftOperand.returnType.Equals(PrimitiveType.INT) &&
                        !leftOperand.returnType.Equals(PrimitiveType.DOUBLE))
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Arithmetic operator can only be applied to numerical types");
                    }
                    break;
                // Modulo operator is accepted only for integer type
                case BinaryOperator.Rem:
                    // Check if type is not integer
                    if (!leftOperand.returnType.Equals(PrimitiveType.INT))
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Reminder operator can only be applied to integer type");
                    }
                    break;
                // Equality and non equality are accepted for all types
                case BinaryOperator.Eq:
                case BinaryOperator.Neq:
                    break;

                // Default case stands for logical operators
                default:
                    // Check if type is not boolean
                    if (!leftOperand.returnType.Equals(PrimitiveType.BOOL))
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "Logical operator can only be applied to boolean type");
                    }
                    break;
            }

            // If both operands are constant expressions, perform constant folding
            if ((leftOperand is ConstantExpression) && (rightOperand is ConstantExpression))
            {
                // Compile time evaluation of expressions is done based on type and operator
                // If type is boolean
                if (leftOperand.returnType.Equals(PrimitiveType.BOOL))
                {
                    switch (op)
                    {
                        // Compute equality
                        case BinaryOperator.Eq:
                            // Cast values to bool, compare and create new constant expression
                            return new ConstantExpression(Primitive.Bool, (bool)(leftOperand as ConstantExpression).value ==
                                (bool)(rightOperand as ConstantExpression).value);
                        // Compute non-equality
                        case BinaryOperator.Neq:
                            // Same as exclusive or for boolean values
                            goto case BinaryOperator.Xor;
                        // Compute logical and
                        case BinaryOperator.And:
                            // Cast values to bool, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Bool, (bool)(leftOperand as ConstantExpression).value &&
                                (bool)(rightOperand as ConstantExpression).value);
                        // Compute logical or
                        case BinaryOperator.Or:
                            // Cast values to bool, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Bool, (bool)(leftOperand as ConstantExpression).value ||
                                (bool)(rightOperand as ConstantExpression).value);
                        // Compute logical exclusive or
                        case BinaryOperator.Xor:
                            // Cast values to bool, apply operation and create constant expression (xor is equivalent to a
                            // non-equality check)
                            return new ConstantExpression(Primitive.Bool, (bool)(leftOperand as ConstantExpression).value !=
                                (bool)(rightOperand as ConstantExpression).value);
                    }
                }
                // If type is double
                else if (leftOperand.returnType.Equals(PrimitiveType.DOUBLE))
                {
                    switch (op)
                    {
                        // Compute equality
                        case BinaryOperator.Eq:
                            // Cast values to double, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (double)(leftOperand as ConstantExpression).value ==
                                (double)(rightOperand as ConstantExpression).value);
                        // Compute non-equality
                        case BinaryOperator.Neq:
                            // Cast values to double, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (double)(leftOperand as ConstantExpression).value !=
                                (double)(rightOperand as ConstantExpression).value);
                        // Compute greater than
                        case BinaryOperator.Gt:
                            // Cast values to double, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (double)(leftOperand as ConstantExpression).value >
                                (double)(rightOperand as ConstantExpression).value);
                        // Compute greater than or equal to
                        case BinaryOperator.Geq:
                            // Cast values to double, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (double)(leftOperand as ConstantExpression).value >=
                                (double)(rightOperand as ConstantExpression).value);
                        // Compute less than
                        case BinaryOperator.Lt:
                            // Cast values to double, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (double)(leftOperand as ConstantExpression).value <
                                (double)(rightOperand as ConstantExpression).value);
                        // Compute less than or equal to
                        case BinaryOperator.Leq:
                            // Cast values to double, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (double)(leftOperand as ConstantExpression).value <=
                                (double)(rightOperand as ConstantExpression).value);

                        // Compute addition
                        case BinaryOperator.Add:
                            // Cast values to double, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Double, ((double)(leftOperand as ConstantExpression).value +
                                (double)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute subtraction
                        case BinaryOperator.Sub:
                            // Cast values to double, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Double, ((double)(leftOperand as ConstantExpression).value -
                                (double)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute multiplication
                        case BinaryOperator.Mul:
                            // Cast values to double, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Double, ((double)(leftOperand as ConstantExpression).value *
                                (double)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute division
                        case BinaryOperator.Div:
                            // Cast values to double, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Double, ((double)(leftOperand as ConstantExpression).value /
                                (double)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                    }
                }
                // If type is integer
                else if (leftOperand.returnType.Equals(PrimitiveType.INT))
                {
                    switch (op)
                    {
                        // Compute equality
                        case BinaryOperator.Eq:
                            // Cast values to int, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (int)(leftOperand as ConstantExpression).value ==
                                (int)(rightOperand as ConstantExpression).value);
                        // Compute non-equality
                        case BinaryOperator.Neq:
                            // Cast values to int, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (int)(leftOperand as ConstantExpression).value !=
                                (int)(rightOperand as ConstantExpression).value);
                        // Compute greater than
                        case BinaryOperator.Gt:
                            // Cast values to int, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (int)(leftOperand as ConstantExpression).value >
                                (int)(rightOperand as ConstantExpression).value);
                        // Compute greater than or equal to
                        case BinaryOperator.Geq:
                            // Cast values to int, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (int)(leftOperand as ConstantExpression).value >=
                                (int)(rightOperand as ConstantExpression).value);
                        // Compute less than
                        case BinaryOperator.Lt:
                            // Cast values to int, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (int)(leftOperand as ConstantExpression).value <
                                (int)(rightOperand as ConstantExpression).value);
                        // Compute less than or equal to
                        case BinaryOperator.Leq:
                            // Cast values to int, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (int)(leftOperand as ConstantExpression).value <=
                                (int)(rightOperand as ConstantExpression).value);

                        // Compute addition
                        case BinaryOperator.Add:
                            // Cast values to int, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Int, ((int)(leftOperand as ConstantExpression).value +
                                (int)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute subtraction
                        case BinaryOperator.Sub:
                            // Cast values to int, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Int, ((int)(leftOperand as ConstantExpression).value -
                                (int)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute multiplication
                        case BinaryOperator.Mul:
                            // Cast values to int, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Int, ((int)(leftOperand as ConstantExpression).value *
                                (int)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute division
                        case BinaryOperator.Div:
                            // Cast values to int, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Int, ((int)(leftOperand as ConstantExpression).value /
                                (int)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                        // Compute modulo
                        case BinaryOperator.Rem:
                            // Cast values to int, apply operation and create constant expression
                            return new ConstantExpression(Primitive.Int, ((int)(leftOperand as ConstantExpression).value %
                                (int)(rightOperand as ConstantExpression).value).ToString(NumberFormatInfo.InvariantInfo));
                    }
                }
                // If type is string
                else if (leftOperand.returnType.Equals(PrimitiveType.STRING))
                {
                    switch (op)
                    {
                        // Compute equality
                        case BinaryOperator.Eq:
                            // Convert values to string, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (leftOperand as ConstantExpression).value.ToString() ==
                                (rightOperand as ConstantExpression).value.ToString());
                        // Compute non-equality
                        case BinaryOperator.Neq:
                            // Convert values to string, apply operation and create constant (boolean) expression
                            return new ConstantExpression(Primitive.Bool, (leftOperand as ConstantExpression).value.ToString() !=
                                (rightOperand as ConstantExpression).value.ToString());

                        // Compute addition
                        case BinaryOperator.Add:
                            // Convert values to string, apply operation and create constant expression
                            return new ConstantExpression(Primitive.String, (leftOperand as ConstantExpression).value.ToString() +
                                (rightOperand as ConstantExpression).value.ToString());
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // If operator is arithmetical or a comparison (not logical)
            if ((int)op < 12)
            {
                // Emit code for operands
                leftOperand.EmitCode(ilGen, scope);
                rightOperand.EmitCode(ilGen, scope);

                // Code is emitted depending on operation
                switch (op)
                {
                    // Arithmetic operators

                    // Emit addition
                    case BinaryOperator.Add:
                        // Check if type is string
                        if (leftOperand.returnType.Equals(PrimitiveType.STRING))
                        {
                            // For strings, call method System.String.Concat(string, string)
                            ilGen.Emit(OpCodes.Call, Type.GetType("System.String").GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                        }
                        else
                        {
                            // For numerical types, emit add
                            ilGen.Emit(OpCodes.Add);
                        }
                        break;
                    // Emit subtraction
                    case BinaryOperator.Sub:
                        ilGen.Emit(OpCodes.Sub);
                        break;
                    // Emit multiplication
                    case BinaryOperator.Mul:
                        ilGen.Emit(OpCodes.Mul);
                        break;
                    // Emit division
                    case BinaryOperator.Div:
                        ilGen.Emit(OpCodes.Div);
                        break;
                    // Emit modulo
                    case BinaryOperator.Rem:
                        ilGen.Emit(OpCodes.Rem);
                        break;

                    // Comparison operators
                    
                    // Emit equality
                    case BinaryOperator.Eq:
                        ilGen.Emit(OpCodes.Ceq);
                        break;
                    // Emit non-equality
                    case BinaryOperator.Neq:
                        // Neq, leg and geq are simulated by negating eq, gt and lt respectively

                        // Emit equality check
                        ilGen.Emit(OpCodes.Ceq);
                        // Load 0 onto the stack
                        ilGen.Emit(OpCodes.Ldc_I4_0);
                        // Check equality
                        ilGen.Emit(OpCodes.Ceq);
                        break;
                    // Emit greater than
                    case BinaryOperator.Gt:
                        ilGen.Emit(OpCodes.Cgt);
                        break;
                    // Emit less than
                    case BinaryOperator.Lt:
                        ilGen.Emit(OpCodes.Clt);
                        break;
                    // Emit less than or equal to
                    case BinaryOperator.Leq:
                        // Emit greater than check
                        ilGen.Emit(OpCodes.Cgt);
                        // Load 0 onto the stack
                        ilGen.Emit(OpCodes.Ldc_I4_0);
                        // Check equality
                        ilGen.Emit(OpCodes.Ceq);
                        break;
                    // Emit greater than or equal to
                    case BinaryOperator.Geq:
                        // Emit less than check
                        ilGen.Emit(OpCodes.Clt);
                        // Load 0 onto the stack
                        ilGen.Emit(OpCodes.Ldc_I4_0);
                        // Check equality
                        ilGen.Emit(OpCodes.Ceq);
                        break;
                }
            }
            // Operator is logical - logical operators are emitted following the rules:
            //      for and: if first operand is false, second operand is not evaluated and expression is considered false
            //      for or: if first operand is true, second operand is not vealuated and expression is considered true
            else
            {
                // Labels needed to skip evaluations if necessary
                Label falseLabel;
                Label trueLabel;
                Label endLabel;

                // Code is emitted depending on operator
                switch (op)
                {
                    // Emit logical and
                    case BinaryOperator.And:
                        // Define labels
                        falseLabel = ilGen.DefineLabel();
                        endLabel = ilGen.DefineLabel();

                        // Emit code for left operand
                        leftOperand.EmitCode(ilGen, scope);
                        // Brake to false if first operand is false
                        ilGen.Emit(OpCodes.Brfalse, falseLabel);

                        // Emit code for right operand
                        rightOperand.EmitCode(ilGen, scope);
                        // Break to end
                        ilGen.Emit(OpCodes.Br, endLabel);

                        // Mark false label
                        ilGen.MarkLabel(falseLabel);
                        // Push 0 onto the stack - brfalse pops value from the stack while br doesn't
                        ilGen.Emit(OpCodes.Ldc_I4_0);

                        // Mark end label
                        ilGen.MarkLabel(endLabel);
                        break;

                    // Emit logical or
                    case BinaryOperator.Or:
                        // Define labels
                        trueLabel = ilGen.DefineLabel();
                        endLabel = ilGen.DefineLabel();

                        // Emit code for left operand
                        leftOperand.EmitCode(ilGen, scope);
                        // Break to true if first operand is true
                        ilGen.Emit(OpCodes.Brtrue, trueLabel);
                        
                        // Emit code for right operand
                        rightOperand.EmitCode(ilGen, scope);
                        // Break to end
                        ilGen.Emit(OpCodes.Br, endLabel);

                        // Mark true label
                        ilGen.MarkLabel(trueLabel);
                        // Push 1 onto the stack - brtrue pops value from the stack while br doesn't
                        ilGen.Emit(OpCodes.Ldc_I4_1);

                        // Mark end label
                        ilGen.MarkLabel(endLabel);
                        break;

                    // Emit logical xor
                    case BinaryOperator.Xor:
                        // Emit code for operands
                        leftOperand.EmitCode(ilGen, scope);
                        rightOperand.EmitCode(ilGen, scope);

                        // Emit xor
                        ilGen.Emit(OpCodes.Xor);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// VariableReferenceExpression represents a reference to a variable
    /// </summary>
    class VariableReferenceExpression : AssignableExpression
    {
        /// <summary>
        /// Creates a new VariableReferenceExpression given a variable name
        /// </summary>
        /// <param name="name">Variable name</param>
        public VariableReferenceExpression(string name)
        {
            this.name = name;
        }

        
        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this expression</param>
        /// <returns></returns>
        public override Expression Evaluate(Scope scope)
        {
            // Set return type by getting variable from symbol table
            returnType = scope.GetVariable(name);

            // Check if no type was returned
            if (returnType == null)
            {
                // Issue error
                Compiler.Compiler.errors.SemErr(t.line, t.col, "Unknown variable reference");
            }

            return this;
        }
        
        /// <summary>
        /// Emits code for this expression
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Variable references are emitted by the symbol table because different variables are referenced with 
            // different instructions and only the symbol table knows how to correctly refer them
            scope.EmitVariableReference(name, ilGen);
        }

        /// <summary>
        /// Emits code for an assignement given the right-side expression of the assignement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this expression</param>
        /// <param name="rightSide">Right-side expression</param>
        public override void EmitAssignement(ILGenerator ilGen, Scope scope, Expression rightSide)
        {
            // Emit code for right side expression
            rightSide.EmitCode(ilGen, scope);

            // Use symbol table to emit variable assignement
            scope.EmitVariableAssignement(name, ilGen);
        }
    }
}