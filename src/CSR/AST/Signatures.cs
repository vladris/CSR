using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace CSR.AST
{
    // Result of match between two signatures
    enum Match
    {
        FirstBest, // First one is better
        SecondBest, // Second one is better
        Ambiguos // Cannot determine which one is better
    }

    /// <summary>
    /// Signature represents a function signature with name, argument types and return type
    /// </summary>
    class Signature
    {
        // Function name
        public string name;

        // Function return type
        public BaseType returnType;

        // List of arguments
        public List<BaseType> arguments;

        /// <summary>
        /// Creates a new Signature
        /// </summary>
        public Signature()
        {
        }

        /// <summary>
        /// Creates a signature from a MethodInfo object (used by Reflection)
        /// </summary>
        /// <param name="methodInfo">MethodInfo object</param>
        public Signature(MethodInfo methodInfo)
        {
            // Set fully qualified name and return type
            name = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
            returnType = BaseType.FromType(methodInfo.ReturnType);
            arguments = new List<BaseType>();

            // Populate argument list
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
            {
                arguments.Add(BaseType.FromType(parameterInfo.ParameterType));
            }
        }

        /// <summary>
        /// Creates a signature from a CallExpression object
        /// </summary>
        /// <param name="callExpr">CallExpresion object</param>
        public Signature(CallExpression callExpr)
        {
            // Set fully qualified name and return type
            name = callExpr.methodName;
            returnType = callExpr.returnType;
            arguments = new List<BaseType>();

            // Populate argument list
            foreach (Expression expr in callExpr.arguments)
            {
                arguments.Add(expr.returnType);
            }
        }

        /// <summary>
        /// Check if this signature is compatible with a given signature
        /// </summary>
        /// <param name="sig">Signature to compare against</param>
        /// <returns></returns>
        public bool IsCompatible(Signature sig)
        {
            // If argument cound is different, return false
            if (arguments.Count != sig.arguments.Count)
            {
                return false;
            }

            // Step through argument list
            for (int i = 0; i < arguments.Count; i++)
            {
                // Check if an argument is null - unsupported CLR type of external method
                if (sig.arguments[i] == null)
                {
                    // Unsupported argument type
                    return false;
                }

                // Check if arguments are different and incompatible
                if (!arguments[i].Equals(sig.arguments[i]) &&
                    !arguments[i].IsCompatible(sig.arguments[i]))
                {
                    // Incompatible argument types
                    return false;
                }
            }

            // Signatures are compatible
            return true;
        }

        /// <summary>
        /// Checks if signature is an exact match to a given signature
        /// </summary>
        /// <param name="sig">Signature to compare against</param>
        /// <returns></returns>
        public bool IsExactMatch(Signature sig)
        {
            // If argument cound is different, return false
            if (arguments.Count != sig.arguments.Count)
            {
                return false;
            }

            // Step through argument list
            for (int i = 0; i < arguments.Count; i++)
            {
                // Check if an argument is null - unsupported CLR type of external method
                if (sig.arguments[i] == null)
                {
                    // Unsupported argument type
                    return false;
                }

                // Check if argument types are equal
                if (!sig.arguments[i].Equals(arguments[i]))
                {
                    // Signatures don't have an exact match
                    return false;
                }
            }

            // Exact signature match
            return true;
        }

        /// <summary>
        /// Compares two signatures agains this signature and returns the best match
        /// </summary>
        /// <param name="sig1">First signature</param>
        /// <param name="sig2">Second signature</param>
        /// <returns></returns>
        public Match BestSignature(Signature sig1, Signature sig2)
        {
            // Assume ambiguos match
            Match result = Match.Ambiguos;

            // Step through argument list
            for (int i = 0; i < arguments.Count; i++)
            {
                // Check if the argument of the first signature has the same type as this one's
                if (arguments[i].Equals(sig1.arguments[i]))
                {
                    // Check if the argument of the second signature has a different type than this one's
                    if (!arguments[i].Equals(sig2.arguments[i]))
                    {
                        // Check if second signature was considered better
                        if (result == Match.SecondBest)
                        {
                            // Match is ambiguos
                            return Match.Ambiguos;
                        }
                        // If second signature wasn't considered better
                        else
                        {
                            // Consider first signature better
                            result = Match.FirstBest;
                        }
                    }
                }
                // If the argument of the first signature has a different type as this one's
                else 
                {
                    // Check if the argument of the second signature has the same type as this one's
                    if (arguments[i].Equals(sig2.arguments[i]))
                    {
                        // Check if first signature was considered better
                        if (result == Match.FirstBest)
                        {
                            // Match is ambigous
                            return Match.Ambiguos;
                        }
                        // If second signature wasn't considered better
                        else
                        {
                            // Consider second signature better
                            result = Match.SecondBest;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a System.String object that represents this Signature (used to create unique signature keys)
        /// </summary>
        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            
            // Start with name
            str.Append(this.name);
            str.Append(":");

            // Append arguments
            foreach (BaseType type in arguments)
            {
                str.Append(type.ToString());
                str.Append(",");
            }

            return str.ToString();
        }
    }
}