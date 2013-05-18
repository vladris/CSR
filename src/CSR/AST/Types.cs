using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.Emit;

using CSR.Parser;

namespace CSR.AST
{
    /// <summary>
    /// Base class from which all types inherit
    /// </summary>
    abstract class BaseType
    {
        // Token is remembered for error reporting purposes
        public Token t;

        /// <summary>
        /// Returns the .NET CLR type that this type maps to
        /// </summary>
        /// <returns>System.Type object</returns>
        public abstract Type ToCLRType();

        /// <summary>
        /// Returns a value indicating whether this type can be implicitly typecasted to
        /// the given type
        /// </summary>
        /// <param name="type">Type object to test against</param>
        /// <returns>True if an implicit typecast is possible</returns>
        public abstract bool IsCompatible(Type type);
        
        /// <summary>
        /// Returns a value indicating whether this type can be implicitly typecasted to
        /// the given type
        /// </summary>
        /// <param name="type">BaseType object to test against</param>
        /// <returns>True if an implicit typecast is possible</returns>
        public abstract bool IsCompatible(BaseType type);

        /// <summary>
        /// Creates a BaseType object from a System.Type object
        /// </summary>
        /// <param name="type">Type object</param>
        /// <returns>A BaseType object</returns>
        public static BaseType FromType(Type type)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                    return PrimitiveType.BOOL;
                case "System.Double":
                    return PrimitiveType.DOUBLE;
                case "System.Int32":
                    return PrimitiveType.INT;
                case "System.String":
                    return PrimitiveType.STRING;
                case "System.Void":
                    return PrimitiveType.VOID;
                default:
                    return PrimitiveType.UNSUPPORTED;
            }
        }
    }

    /// <summary>
    /// Primitive types supported
    /// </summary>
    enum Primitive
    {
        Bool,
        Double,
        Int,
        String,
        Void,
        Unsupported
    }

    /// <summary>
    /// Represents a primitive type
    /// </summary>
    class PrimitiveType : BaseType
    {
        public Primitive type;

        // Immutable objects of type PrimitiveType for each primitive
        public static readonly PrimitiveType BOOL = new PrimitiveType(Primitive.Bool);
        public static readonly PrimitiveType INT = new PrimitiveType(Primitive.Int);
        public static readonly PrimitiveType DOUBLE = new PrimitiveType(Primitive.Double);
        public static readonly PrimitiveType STRING = new PrimitiveType(Primitive.String);
        public static readonly PrimitiveType VOID = new PrimitiveType(Primitive.Void);
        public static readonly PrimitiveType UNSUPPORTED = new PrimitiveType(Primitive.Unsupported);

        /// <summary>
        /// Creates a PrimitiveType object given a primitive
        /// </summary>
        /// <param name="type"></param>
        public PrimitiveType(Primitive type)
        {
            this.type = type;
        }

        /// <summary>
        /// Converts this instance to the CLR equivalent type
        /// </summary>
        /// <returns></returns>
        public override Type ToCLRType()
        {
            switch (type)
            {
                case Primitive.Bool:
                    return Type.GetType("System.Boolean");
                case Primitive.Double:
                    return Type.GetType("System.Double");
                case Primitive.Int:
                    return Type.GetType("System.Int32");
                case Primitive.String:
                    return Type.GetType("System.String");
                default:
                    return Type.GetType("System.Void");
            }
        }

        // Overriden to check primitive type equality
        public override bool Equals(object obj)
        {
            PrimitiveType pType = obj as PrimitiveType;

            if (pType == null)
            {
                return false;
            }

            return this.type == pType.type;
        }

        /// <summary>
        /// Returns a value indicating whether this type can be implicitly typecasted to
        /// the given type
        /// </summary>
        /// <param name="type">Type object to test against</param>
        /// <returns>True if an implicit typecast is possible</returns>
        public override bool IsCompatible(Type type)
        {
            // Int can be implicitly typecasted to Double
            if (this.type == Primitive.Int && type.FullName == "System.Double")
            {
                return true;
            }
            // Any future type compatibility implementation should be added here
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a value indicating whether this type can be implicitly typecasted to
        /// the given type
        /// </summary>
        /// <param name="type">BaseType object to test against</param>
        /// <returns>True if an implicit typecast is possible</returns>        
        public override bool IsCompatible(BaseType type)
        {
            // Convert to CLR type and perform check
            return IsCompatible(type.ToCLRType());
        }

        /// <summary>
        /// Returns a System.String object representing the primitive type of this object
        /// </summary>
        /// <returns>Primitve type of this object</returns>
        public override string ToString()
        {
            return type.ToString();
        }

        // Overriden because the Equals method was overriden
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Represents an array type
    /// </summary>
    class ArrayType : BaseType
    {
        // Primitive type contained within the array
        public PrimitiveType type;

        // Array dimensions and sizes
        public int dimensions;
        public List<int> sizes;

        /// <summary>
        /// Creates a new ArrayType given a BaseType and the first dimension
        /// </summary>
        /// <param name="type">PrimitiveType contained within the array</param>
        /// <param name="firstSize">First dimension of the array</param>
        public ArrayType(BaseType type, string firstSize)
        {
            // Initialize members
            this.type = type as PrimitiveType;
            dimensions = 1;
            sizes = new List<int>();

            // Add first dimension
            sizes.Add(int.Parse(firstSize));
        }

        /// <summary>
        /// Add a new dimension to the array
        /// </summary>
        /// <param name="size">Size of the dimension</param>
        public void AddDimension(string size)
        {
            // Add dimension
            dimensions++;
            sizes.Add(int.Parse(size));
        }

        /// <summary>
        /// Arrays are considered equal if they have equal dimensions
        /// </summary>
        public override bool Equals(object obj)
        {
            ArrayType arrType = obj as ArrayType;
            if (arrType == null)
            {
                return false;
            }

            return dimensions == arrType.dimensions;
        }

        /// <summary>
        /// Overriden - arrays cannot be compatible types because they cannot be typecasted
        /// </summary>
        public override bool IsCompatible(BaseType type)
        {
            return false;
        }

        /// <summary>
        /// Overriden - arrays cannot be compatible types because they cannot be typecasted
        /// </summary>
        public override bool IsCompatible(Type type)
        {
            return false;
        }

        /// <summary>
        /// Converts the array to a CLR array with similar contained type and dimensions
        /// </summary>
        /// <returns>Equivalent CLR Type</returns>
        public override Type ToCLRType()
        {
            return Type.GetType(ToString());
        }

        /// <summary>
        /// Converts the array to a string representation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            // The string representation of a CLR array type is type[,,,] with a comma for each dimension
            // Jagged arrays are not supported (jagged array are type[][][])

            StringBuilder result = new StringBuilder();

            // Convert contained primitive type to string
            result.Append(type.ToCLRType().ToString());

            result.Append("[");

            // Add a comma for each dimension - 1
            for (int i = 1; i < dimensions; i++)
            {
                result.Append(",");
            }

            result.Append("]");
            
            return result.ToString();
        }

        // Overriden because Equals was overriden
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Emits an array instantiation
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        public void EmitInstance(ILGenerator ilGen)
        {
            // The size of each dimension is pushed on the stack
            foreach (int size in sizes)
            {
                ilGen.Emit(OpCodes.Ldc_I4, size);
            }

            // Emit newobj using the constructor of the equivalent CLR type
            ilGen.Emit(OpCodes.Newobj, ToCLRType().GetConstructors()[0]);
        }

        /// <summary>
        /// Emits a call to the array's Get method which returns a value or subarray within the array
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        public void EmitGet(ILGenerator ilGen)
        {
            // Emit call using the Get method of the equivalent CLR type
            ilGen.Emit(OpCodes.Call, ToCLRType().GetMethod("Get"));
        }

        /// <summary>
        /// Emits a call to the array's Set method which sets a value within the array
        /// </summary>
        /// <param name="ilGen">IL generator object</param>
        public void EmitSet(ILGenerator ilGen)
        {
            // Emit call using the Set method of the equivalent CLR type
            ilGen.Emit(OpCodes.Call, ToCLRType().GetMethod("Set"));
        }
    }
}
