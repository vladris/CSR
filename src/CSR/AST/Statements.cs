using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

using CSR.Parser;

namespace CSR.AST
{
    /// <summary>
    /// Abstract Statement class from which all statements inherit
    /// </summary>
    abstract class Statement
    {
        // Token is remembered for error reporting
        public Token t;

        /// <summary>
        /// Creates a new Statement
        /// </summary>
        public Statement()
        {
            returns = false;
        }

        /// <summary>
        /// Evaluates this node and all children recursively 
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns>Statement object after evaluation</returns>
        public abstract Statement Evaluate(Scope scope);

        /// <summary>
        /// True if this node or any of its children contains a return statement which will be executed in any
        /// given context
        /// </summary>
        public bool returns;

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public abstract void EmitCode(ILGenerator ilGen, Scope scope);
    }

    /// <summary>
    /// BlockStatements wrap a list of inner statements
    /// </summary>
    class BlockStatement : Statement
    {
        // List of inner statements
        public List<Statement> statements;

        /// <summary>
        /// Creates a new BlockStatement
        /// </summary>
        public BlockStatement()
            : base()
        {
            statements = new List<Statement>();
        }

        /// <summary>
        /// Adds a statement to the list of inner statements
        /// </summary>
        /// <param name="stmt">Statement to add</param>
        public void AddStatement(Statement stmt)
        {
            statements.Add(stmt);
        }

        /// <summary>
        /// Evaluates this node and all its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            // Only evaluation needed for block statements is dead code elimination - statements following a return statement
            // which will never be executed

            int i = 0;

            // Iterate through all inner statements
            while (i < statements.Count)
            {
                // Evaluate inner statement
                statements[i] = statements[i].Evaluate(scope);

                // If statement is null after evaluation, drop it
                if (statements[i] == null)
                {
                    statements.RemoveAt(i);

                    continue;
                }

                // Check if this statment is or contains a return statement
                if (statements[i].returns)
                {
                    // If so, mark this statement as returning too
                    returns = true;

                    // Check for code beyond this statment
                    if (i < statements.Count - 1)
                    {
                        // Unreachable code detected - issue a warning
                        Compiler.Compiler.errors.Warning(statements[i + 1].t.line, statements[i + 1].t.col, "unreachable code detected");

                        // Remove unreachable code
                        while (i < statements.Count - 1)
                        {
                            statements.RemoveAt(i + 1);
                        }
                    }
                }

                i++;
            }

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit code for each inner statement
            foreach (Statement stmt in statements)
            {
                stmt.EmitCode(ilGen, scope);
            }
        }
    }

    /// <summary>
    /// CallStatement represents a method call expression for which the return value is ignored
    /// </summary>
    class CallStatement : Statement
    {
        // Contained call expression
        public CallExpression expr;

        /// <summary>
        /// Creates a new CallStatement given a CallExpression
        /// </summary>
        /// <param name="expr">CallExpression object</param>
        public CallStatement(Expression expr)
            : base()
        {
            this.expr = expr as CallExpression;
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            // Just evaluate inner call expression
            expr = expr.Evaluate(scope) as CallExpression;

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit cod for the inner call expression
            expr.EmitCode(ilGen, scope);

            // If call returns an unsupported type or a non-void value, pop it from the stack
            if (expr.returnType.Equals(PrimitiveType.UNSUPPORTED) || !expr.returnType.Equals(PrimitiveType.VOID))
            {
                ilGen.Emit(OpCodes.Pop);
            }
        }
    }

    /// <summary>
    /// ReturnStatement represents a return statement which imediately exists the current function
    /// </summary>
    class ReturnStatement : Statement
    {
        // Expression to return from the function
        public Expression expr;

        /// <summary>
        /// Creates a new ReturnStatement
        /// </summary>
        public ReturnStatement()
            : base()
        {
            returns = true;
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            LocalScope localScope = scope as LocalScope;

            // If expr is not null, we must match return value with function return type
            if (expr != null)
            {
                // Evaluate inner expression
                expr.Evaluate(scope);

                // If return type is different than function return type
                if (!expr.returnType.Equals(localScope.returnType))
                {
                    // Check if an implicit typecast exists
                    if (expr.returnType.IsCompatible(localScope.returnType))
                    {
                        // Create typecast
                        CastExpression newExpr = new CastExpression(localScope.returnType);
                        newExpr.operand = expr;
                        this.expr = newExpr;
                    }
                    else
                    {
                        // Issue error
                        Compiler.Compiler.errors.SemErr(t.line, t.col, "invalid return type");
                    }
                }
            }
            else
            {
                // If function returns void but we provide a different type
                if (localScope.returnType.ToCLRType() != Type.GetType("System.Void"))
                {
                    // Issue error
                    Compiler.Compiler.errors.SemErr(t.line, t.col, "function should return " + localScope.returnType);
                }
            }

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // If expression is not null, issue code for the expression
            if (expr != null)
            {
                expr.EmitCode(ilGen, scope);
            }

            // Emit ret to exit current method
            ilGen.Emit(OpCodes.Ret);
        }
    }

    /// <summary>
    /// AssignementStatements have a valid, assignable left-side expression and a right-side
    /// expression to assign
    /// </summary>
    class AssignementStatement : Statement
    {
        // Expressions
        public Expression leftSide;
        public Expression rightSide;

        /// <summary>
        /// Creates a new AssignementExpression given a left-side expression and a right-side expression
        /// </summary>
        /// <param name="leftSide">Left-side expression</param>
        /// <param name="rightSide">Right-side expression</param>
        public AssignementStatement(Expression leftSide, Expression rightSide)
            : base()
        {
            this.leftSide = leftSide;
            this.rightSide = rightSide;
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            // Check if left-side expression is assignable
            if (!(leftSide is AssignableExpression))
            {
                // Issue error
                Compiler.Compiler.errors.SemErr(t.line, t.col, "Left side cannot be assigned to");

                return this;
            }

            // Evaluate both expressions
            leftSide = leftSide.Evaluate(scope);
            rightSide = rightSide.Evaluate(scope);

            return this;
        }


        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Call the EmitAssignement method of the AssignableExpression object
            (leftSide as AssignableExpression).EmitAssignement(ilGen, scope, rightSide);
        }
    }

    /// <summary>
    /// IfStatements represent conditional statements defined by a condition expression, an if-branch statement and an 
    /// optional else-branch statement
    /// </summary>
    class IfStatement : Statement
    {
        // Condition expression and branch statements
        public Expression condition;
        public Statement ifBranch;
        public Statement elseBranch;

        /// <summary>
        /// Creates a new IfStatement
        /// </summary>
        public IfStatement()
            : base()
        {
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            // Evaluate the condition
            condition = condition.Evaluate(scope);

            // Evaluate the if branch
            ifBranch = ifBranch.Evaluate(scope);

            // Evaluate the else branch if it exists
            if (elseBranch != null)
            {
                elseBranch = elseBranch.Evaluate(scope);

                // Propagate return state if both branches return
                // This is the only case in which we can make sure code following the statement won't be reached
                returns = ifBranch.returns && elseBranch.returns;
            }

            // Perform dead code elimination if the value of the condition can be evaluated at compile time
            if (condition is ConstantExpression)
            {
                // If condition always holds, replace statement with the if branch
                if ((condition as ConstantExpression).IsTrue())
                    return ifBranch;
                else
                    // If an else branch exists, replace statemnt with it, if not, drop statement
                    if (elseBranch != null)
                        return elseBranch;
                    else
                        return null;
            }

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Emit code for the condition expression
            condition.EmitCode(ilGen, scope);
            
            // Define labels
            Label elseLabel = ilGen.DefineLabel();
            Label endLabel = ilGen.DefineLabel();

            if (elseBranch != null)
            {
                // If else branch exists, jump to it if top stack value is 0
                ilGen.Emit(OpCodes.Brfalse, elseLabel);
            }
            else
            {
                // If not, jump at the end of the statement
                ilGen.Emit(OpCodes.Brfalse, endLabel);
            }

            // Emit code for if branch
            ifBranch.EmitCode(ilGen, scope);

            if (elseBranch != null)
            {
                // If else branch exists, emit jump past it to the end of the statement
                ilGen.Emit(OpCodes.Br, endLabel);

                // Mark else branch label
                ilGen.MarkLabel(elseLabel);

                // Emit code for else branch
                elseBranch.EmitCode(ilGen, scope);
            }

            // Mark end label
            ilGen.MarkLabel(endLabel);
        }
    }

    /// <summary>
    /// WhileStatements represent a repetitive statement and a conditional expression, the condition being evaluated first
    /// </summary>
    class WhileStatement : Statement
    {
        // Condition expression and repetitive statement
        public Expression condition;
        public Statement body;

        /// <summary>
        /// Creates a new WhileStatement
        /// </summary>
        public WhileStatement()
            : base()
        {
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            // Evaluate condition
            condition = condition.Evaluate(scope);

            // Evaluate repetitive statement
            body = body.Evaluate(scope);

            // Perform dead code elimination if the value of the condition can be evaluated at compile time
            if (condition is ConstantExpression)
            {
                // If condition is always false, drop statement
                if (!(condition as ConstantExpression).IsTrue())
                    return null;
            }

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Define labels
            Label loopLabel = ilGen.DefineLabel();
            Label endLabel = ilGen.DefineLabel();

            // Mark the start of the loop
            ilGen.MarkLabel(loopLabel);

            // Emit code for the condition
            condition.EmitCode(ilGen, scope);

            // If the value on top of the stack evaluates to 0, jump to the end of the statement
            ilGen.Emit(OpCodes.Brfalse, endLabel);
            
            // Emit code for the repetitive statement
            body.EmitCode(ilGen, scope);

            // Jump to the beginning of the statement
            ilGen.Emit(OpCodes.Br, loopLabel);

            // Mark end label
            ilGen.MarkLabel(endLabel);
        }
    }

    /// <summary>
    /// DoWhileStatements represent a repetitive statement and a conditional expression, the condition being evaluated
    /// after the repetitive statement runs once
    /// </summary>
    class DoWhileStatement : Statement
    {
        // Condition expression and repetitive statement
        public Expression condition;
        public Statement body;

        /// <summary>
        /// Creates a new DoWhileStatement
        /// </summary>
        public DoWhileStatement()
            : base()
        {
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            // Evaluate condition expression
            condition = condition.Evaluate(scope);

            // Evaluate repetitive statement
            body = body.Evaluate(scope);

            // Perform dead code elimination if the value of the condition can be evaluated at compile time
            if (condition is ConstantExpression)
            {
                // If it is always false, replace the statement with the inner repetitive statement
                if (!(condition as ConstantExpression).IsTrue())
                    return body;
            }

            // Propagate return state - if inner statement returns, no code following this statement
            // will be reached
            returns = body.returns;

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Define label
            Label loopLabel = ilGen.DefineLabel();

            // Mark label at beginning of statement
            ilGen.MarkLabel(loopLabel);

            // Emit code for the inner statement
            body.EmitCode(ilGen, scope);
            
            // Emit code for the condition expression
            condition.EmitCode(ilGen, scope);

            // If the value on top of the stack is different than 0, jump to beginning of statement
            ilGen.Emit(OpCodes.Brtrue, loopLabel);
        }
    }

    /// <summary>
    /// Specifies weather the parameter of the for loop is increased or decreased
    /// </summary>
    enum ForDirection
    {
        Up,
        Down
    }

    /// <summary>
    /// ForStatements represent a statement with an integer parameter starting at a given value, while the parameter
    /// is different than another given value, a repetitive statement is executed then the parameter is increased
    /// or decreased
    /// </summary>
    class ForStatement : Statement
    {
        // Parameter - must be an assignable expression
        public Expression variable;

        // Expressions representing the initial and final values
        public Expression initial;
        public Expression final;

        // Repetitive statement
        public Statement body;

        // Specifies if the parameter is increased or decreased
        public ForDirection direction;

        /// <summary>
        /// Creates a new ForStatement
        /// </summary>
        public ForStatement()
            : base()
        {
        }

        /// <summary>
        /// Evaluates this node and all of its children
        /// </summary>
        /// <param name="scope">The scope of this statement</param>
        /// <returns></returns>
        public override Statement Evaluate(Scope scope)
        {
            if (!(variable is AssignableExpression))
            {
                // If variable is not assignable expression, issue error
                Compiler.Compiler.errors.SemErr(t.line, t.col, "Invalid variable for FOR statement");

                return this;
            }

            // Evaluate all child nodes
            variable = variable.Evaluate(scope);
            initial = initial.Evaluate(scope);
            final = final.Evaluate(scope);
            body = body.Evaluate(scope);

            return this;
        }

        /// <summary>
        /// Emits code for this statement
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        /// <param name="scope">The scope of this statement</param>
        public override void EmitCode(ILGenerator ilGen, Scope scope)
        {
            // Define labels
            Label loopLabel = ilGen.DefineLabel();
            Label endLabel = ilGen.DefineLabel();

            // Call EmitAssignement for parameter, assigning it the initial expression
            (variable as AssignableExpression).EmitAssignement(ilGen, scope, initial);

            // Mark beginning of loop
            ilGen.MarkLabel(loopLabel);

            // Emit parameter code (load value onto the stack)
            variable.EmitCode(ilGen, scope);
            // Emit final expression (load expression value onto the stack)
            final.EmitCode(ilGen, scope);

            // If parameter is increased
            if (direction == ForDirection.Up)
            {
                // If parameter is greater than final, jump to the end of the statement
                ilGen.Emit(OpCodes.Bgt, endLabel);
            }
            // If parameter is decreased
            else
            {
                // If parameter is less than final, jump to the end of the statement
                ilGen.Emit(OpCodes.Blt, endLabel);
            }

            // Emit code for repetitive statement
            body.EmitCode(ilGen, scope);

            // Increment or decrement parameter:
            // Create a new binary expression with the parameter as left operand 
            BinaryExpression step = new BinaryExpression(variable);

            // If parameter is increasing, operator is Add, if it is decreasing, operator is Sub
            step.op = direction == ForDirection.Up ? BinaryOperator.Add : BinaryOperator.Sub;

            // Right operand is 1
            step.rightOperand = new ConstantExpression(Primitive.Int, "1");

            // Emit assignement - parameter is assigned the result of the binary expression
            (variable as AssignableExpression).EmitAssignement(ilGen, scope, step);

            // Emit unconditional break to beginning of loop
            ilGen.Emit(OpCodes.Br, loopLabel);

            // Mark end label
            ilGen.MarkLabel(endLabel);
        }
    }
}
