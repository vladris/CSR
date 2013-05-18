using System.Collections;
using System.Collections.Generic;

using CSR.AST;

using System;


namespace CSR.Parser {

using VariableDeclarationList = List<VariableDeclaration>;

public class Parser {
	const int _EOF = 0;
	const int _ident = 1;
	const int _intCon = 2;
	const int _realCon = 3;
	const int _stringCon = 4;
	const int _program = 5;
	const int _function = 6;
	const int _begin = 7;
	const int _end = 8;
	const int _for = 9;
	const int _to = 10;
	const int _downto = 11;
	const int _do = 12;
	const int _while = 13;
	const int _var = 14;
	const int _if = 15;
	const int _else = 16;
	const int _and = 17;
	const int _or = 18;
	const int _xor = 19;
	const int _bool = 20;
	const int _int = 21;
	const int _double = 22;
	const int _string = 23;
	const int _void = 24;
	const int _null = 25;
	const int _false = 26;
	const int _true = 27;
	const int _return = 28;
	const int _assgn = 29;
	const int _colon = 30;
	const int _comma = 31;
	const int _dot = 32;
	const int _eq = 33;
	const int _gt = 34;
	const int _gteq = 35;
	const int _lbrace = 36;
	const int _lbrack = 37;
	const int _lpar = 38;
	const int _lt = 39;
	const int _minus = 40;
	const int _not = 41;
	const int _plus = 42;
	const int _rbrace = 43;
	const int _rbrack = 44;
	const int _rpar = 45;
	const int _scolon = 46;
	const int _times = 47;
	const int maxT = 52;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

const int maxTerminals = 160;  // set size

static BitArray NewSet(params int[] values) {
  BitArray a = new BitArray(maxTerminals);
  foreach (int x in values) a[x] = true;
  return a;
}

static BitArray
  unaryOp      = NewSet(_minus, _not, _true, _false),
  typeKW       = NewSet(_bool, _string, _int,_double),
  unaryHead    = NewSet(_plus, _minus, _not, _times, _and),
  castFollower = NewSet(_not, _lpar, _ident,
                 /* literals */
                 _intCon, _realCon, _stringCon
                 ),

  keyword      = NewSet(_program, _function, _begin, _end, _for, _to, _downto, _do, _while, _var, _if, _else, _and, _or, _xor, _bool, _int, _double, _string, _void, _null, _false, _true, _return),
  assgnOps     = NewSet(_assgn);

/*---------------------------- auxiliary methods ------------------------*/

void Error (string s) {
  if (errDist >= minErrDist) errors.SemErr(la.line, la.col, s);
  errDist = 0;
}

// Return the n-th token after the current lookahead token
Token Peek (int n) {
  scanner.ResetPeek();
  Token x = la;
  while (n > 0) { x = scanner.Peek(); n--; }
  return x;
}

/*------------------------------------------------------------------------*
 *----- SCANNER DESCRIPTION ----------------------------------------------*
 *------------------------------------------------------------------------*/



	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void V() {
		Expect(5);
		Expect(1);
		CSR.Compiler.Compiler.staticScope.name = t.val; 
		Expect(46);
		while (la.kind == 6 || la.kind == 14) {
			if (la.kind == 6) {
				FunctionDeclaration decl; 
				FunctionDeclaration(out decl, CSR.Compiler.Compiler.staticScope);
				CSR.Compiler.Compiler.staticScope.AddFunction(decl); 
			} else {
				VariableDeclarationList decls; 
				VariableDeclarations(out decls);
				CSR.Compiler.Compiler.staticScope.AddVariables(decls); 
			}
		}
		FunctionDeclaration main; 
		ProgramBody(out main);
		CSR.Compiler.Compiler.staticScope.entryPoint = main; 
	}

	void FunctionDeclaration(out FunctionDeclaration decl, Scope parentScope) {
		Statement stmt; 
		BaseType type;
		VariableDeclarationList decls;
		
		Expect(6);
		Expect(1);
		decl = new FunctionDeclaration(t, parentScope); 
		Expect(38);
		if (StartOf(1)) {
			Type(out type);
			Expect(1);
			decl.AddArgument(t, type); 
			while (la.kind == 31) {
				Get();
				Type(out type);
				Expect(1);
				decl.AddArgument(t, type); 
			}
		}
		Expect(45);
		if (la.kind == 30) {
			Get();
			Type(out type);
			decl.returnType = type; 
		}
		while (la.kind == 14) {
			VariableDeclarations(out decls);
			decl.localScope.AddVariables(decls); 
		}
		BlockStatement(out stmt);
		decl.body = stmt as BlockStatement; 
	}

	void VariableDeclarations(out VariableDeclarationList decls) {
		decls = new VariableDeclarationList(); 
		BaseType type;
		
		Expect(14);
		Type(out type);
		Expect(1);
		decls.Add(new VariableDeclaration(t, type)); 
		while (la.kind == 31) {
			Get();
			Expect(1);
			decls.Add(new VariableDeclaration(t, type)); 
		}
		Expect(46);
		while (StartOf(1)) {
			Type(out type);
			Expect(1);
			decls.Add(new VariableDeclaration(t, type)); 
			while (la.kind == 31) {
				Get();
				Expect(1);
				decls.Add(new VariableDeclaration(t, type)); 
			}
			Expect(46);
		}
	}

	void ProgramBody(out FunctionDeclaration main) {
		Statement stmt; 
		main = new FunctionDeclaration("Main", CSR.Compiler.Compiler.staticScope);
		
		BlockStatement(out stmt);
		main.body = stmt as BlockStatement; 
	}

	void BlockStatement(out Statement stmt) {
		BlockStatement blockStmt = new BlockStatement(); 
		Statement containedStmt;
		
		Expect(7);
		blockStmt.t = t; 
		while (StartOf(2)) {
			Statement(out containedStmt);
			blockStmt.AddStatement(containedStmt); 
		}
		Expect(8);
		stmt = blockStmt; 
	}

	void Type(out BaseType type) {
		Primitive(out type);
		Token keepT = type.t; 
		if (la.kind == 37) {
			Get();
			Expect(2);
			type = new ArrayType(type, t.val); 
			type.t = keepT; 
			while (la.kind == 31) {
				Get();
				Expect(2);
				(type as ArrayType).AddDimension(t.val); 
			}
			Expect(44);
		}
	}

	void Primitive(out BaseType type) {
		type = null; 
		if (la.kind == 21) {
			Get();
			type = PrimitiveType.INT; 
			type.t = t;
			
		} else if (la.kind == 22) {
			Get();
			type = PrimitiveType.DOUBLE;
			type.t = t;
			
		} else if (la.kind == 23) {
			Get();
			type = PrimitiveType.STRING;
			type.t = t;
			
		} else if (la.kind == 20) {
			Get();
			type = PrimitiveType.BOOL;
			type.t = t;
			
		} else SynErr(53);
	}

	void Statement(out Statement stmt) {
		stmt = null; 
		switch (la.kind) {
		case 7: {
			BlockStatement(out stmt);
			break;
		}
		case 1: case 2: case 3: case 4: case 26: case 27: case 36: case 38: case 40: case 41: {
			CallOrAssgStatement(out stmt);
			break;
		}
		case 28: {
			ReturnStatement(out stmt);
			break;
		}
		case 15: {
			IfStatement(out stmt);
			break;
		}
		case 13: {
			WhileStatement(out stmt);
			break;
		}
		case 12: {
			DoWhileStatement(out stmt);
			break;
		}
		case 9: {
			ForStatement(out stmt);
			break;
		}
		default: SynErr(54); break;
		}
	}

	void CallOrAssgStatement(out Statement stmt) {
		Expression expr; 
		stmt = null;
		
		Expression(out expr);
		if (la.kind == 29) {
			Get();
			Expression rightSide; 
			Expression(out rightSide);
			stmt = new AssignementStatement(expr, rightSide); 
		}
		Expect(46);
		if (stmt == null)
		stmt = new CallStatement(expr); 
		 stmt.t = expr.t;
		
	}

	void ReturnStatement(out Statement stmt) {
		Expect(28);
		stmt = new ReturnStatement(); 
		stmt.t = t;									
		
		if (StartOf(3)) {
			Expression(out (stmt as ReturnStatement).expr);
		}
		Expect(46);
	}

	void IfStatement(out Statement stmt) {
		stmt = new IfStatement(); 
		Expect(15);
		stmt.t = t; 
		Expect(38);
		Expression(out (stmt as IfStatement).condition);
		Expect(45);
		Statement(out (stmt as IfStatement).ifBranch);
		if (la.kind == 16) {
			Get();
			Statement(out (stmt as IfStatement).elseBranch);
		}
	}

	void WhileStatement(out Statement stmt) {
		stmt = new WhileStatement(); 
		Expect(13);
		stmt.t = t; 
		Expect(38);
		Expression(out (stmt as WhileStatement).condition);
		Expect(45);
		Statement(out (stmt as WhileStatement).body);
	}

	void DoWhileStatement(out Statement stmt) {
		stmt = new DoWhileStatement(); 
		Expect(12);
		stmt.t = t; 
		Statement(out (stmt as DoWhileStatement).body);
		Expect(13);
		Expect(38);
		Expression(out (stmt as DoWhileStatement).condition);
		Expect(45);
	}

	void ForStatement(out Statement stmt) {
		stmt = new ForStatement(); 
		Expect(9);
		stmt.t = t; 
		Expression(out (stmt as ForStatement).variable);
		Expect(29);
		Expression(out (stmt as ForStatement).initial);
		if (la.kind == 10) {
			Get();
			(stmt as ForStatement).direction = ForDirection.Up; 
		} else if (la.kind == 11) {
			Get();
			(stmt as ForStatement).direction = ForDirection.Down; 
		} else SynErr(55);
		Expression(out (stmt as ForStatement).final);
		Expect(12);
		Statement(out (stmt as ForStatement).body);
	}

	void Expression(out Expression expr) {
		expr = null; 
		UnaryExpr(out expr);
		Expression leftOperand = expr; 
		EqExpr(out expr, leftOperand);
	}

	void UnaryExpr(out Expression expr) {
		expr = null; 
		switch (la.kind) {
		case 40: {
			Get();
			expr = new UnaryExpression(UnaryOperator.UMinus);
			expr.t = t; 
			
			UnaryExpr(out (expr as UnaryExpression).operand);
			break;
		}
		case 41: {
			Get();
			expr = new UnaryExpression(UnaryOperator.Not); 
			expr.t = t;
			
			UnaryExpr(out (expr as UnaryExpression).operand);
			break;
		}
		case 2: case 3: case 4: case 26: case 27: {
			ConstantExpression(out expr);
			break;
		}
		case 36: {
			Get();
			Expect(21);
			expr = new CastExpression(PrimitiveType.INT); 
			expr.t = t;
			
			Expect(43);
			UnaryExpr(out (expr as CastExpression).operand);
			break;
		}
		case 1: {
			Get();
			expr = new VariableReferenceExpression(t.val); 
			expr.t = t;
			
			while (la.kind == 32) {
				Get();
				Expect(1);
				(expr as VariableReferenceExpression).name += "." + t.val; 
			}
			if (la.kind == 38) {
				Get();
				expr = new CallExpression((expr as VariableReferenceExpression).name, expr.t); 
				Expression argument;	
				
				if (StartOf(3)) {
					Expression(out argument);
					(expr as CallExpression).AddArgument(argument); 
					while (la.kind == 31) {
						Get();
						Expression(out argument);
						(expr as CallExpression).AddArgument(argument); 
					}
				}
				Expect(45);
			}
			if (la.kind == 37) {
				Get();
				expr = new IndexerExpression(expr); 
				Expression indexer;	
				
				Expression(out indexer);
				(expr as IndexerExpression).AddIndexer(indexer); 
				while (la.kind == 31) {
					Get();
					Expression(out indexer);
					(expr as IndexerExpression).AddIndexer(indexer); 
				}
				Expect(44);
			}
			break;
		}
		case 38: {
			Get();
			Expression(out expr);
			Expect(45);
			break;
		}
		default: SynErr(56); break;
		}
	}

	void EqExpr(out Expression expr, Expression leftOperand) {
		expr = null;
		Expression rightOperand;
		
		LogExpr(out expr, leftOperand);
		while (StartOf(4)) {
			expr = new BinaryExpression(expr); 
			switch (la.kind) {
			case 33: {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Eq; 
				break;
			}
			case 48: {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Neq; 
				break;
			}
			case 34: {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Gt; 
				break;
			}
			case 39: {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Lt; 
				break;
			}
			case 35: {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Geq; 
				break;
			}
			case 49: {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Leq; 
				break;
			}
			}
			expr.t = t; 
			UnaryExpr(out rightOperand);
			LogExpr(out (expr as BinaryExpression).rightOperand, rightOperand);
		}
	}

	void ConstantExpression(out Expression expr) {
		expr = null; 
		if (la.kind == 4) {
			Get();
			expr = new ConstantExpression(CSR.AST.Primitive.String, t); 
		} else if (la.kind == 2) {
			Get();
			expr = new ConstantExpression(CSR.AST.Primitive.Int, t); 
		} else if (la.kind == 3) {
			Get();
			expr = new ConstantExpression(CSR.AST.Primitive.Double, t); 
		} else if (la.kind == 27) {
			Get();
			expr = new ConstantExpression(CSR.AST.Primitive.Bool, t); 
		} else if (la.kind == 26) {
			Get();
			expr = new ConstantExpression(CSR.AST.Primitive.Bool, t); 
		} else SynErr(57);
	}

	void LogExpr(out Expression expr, Expression leftOperand) {
		expr = null; 
		Expression rightOperand;
		
		AddExpr(out expr, leftOperand);
		while (la.kind == 17 || la.kind == 18 || la.kind == 19) {
			expr = new BinaryExpression(expr); 
			if (la.kind == 17) {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.And; 
			} else if (la.kind == 18) {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Or; 
			} else {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Xor; 
			}
			expr.t = t; 
			UnaryExpr(out rightOperand);
			AddExpr(out (expr as BinaryExpression).rightOperand, rightOperand);
		}
	}

	void AddExpr(out Expression expr, Expression leftOperand) {
		expr = null; 
		Expression rightOperand;
		
		MulExpr(out expr, leftOperand);
		while (la.kind == 40 || la.kind == 42) {
			expr = new BinaryExpression(expr); 
			if (la.kind == 42) {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Add; 
			} else {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Sub; 
			}
			expr.t = t; 
			UnaryExpr(out rightOperand);
			MulExpr(out (expr as BinaryExpression).rightOperand, rightOperand);
		}
	}

	void MulExpr(out Expression expr, Expression leftOperand) {
		expr = leftOperand; 
		while (la.kind == 47 || la.kind == 50 || la.kind == 51) {
			expr = new BinaryExpression(leftOperand); 
			if (la.kind == 47) {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Mul; 
			} else if (la.kind == 50) {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Div; 
			} else {
				Get();
				(expr as BinaryExpression).op = BinaryOperator.Rem; 
			}
			expr.t = t; 
			UnaryExpr(out (expr as BinaryExpression).rightOperand);
		}
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		V();

    Expect(0);
	}
	
	bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,T,T, T,x,x,T, x,T,x,x, T,T,x,T, x,x,x,x, x,x,x,x, x,x,T,T, T,x,x,x, x,x,x,x, T,x,T,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,T,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, T,x,T,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, x,x,x,T, x,x,x,x, x,x,x,x, T,T,x,x, x,x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
  public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text
  
	public void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "intCon expected"; break;
			case 3: s = "realCon expected"; break;
			case 4: s = "stringCon expected"; break;
			case 5: s = "program expected"; break;
			case 6: s = "function expected"; break;
			case 7: s = "begin expected"; break;
			case 8: s = "end expected"; break;
			case 9: s = "for expected"; break;
			case 10: s = "to expected"; break;
			case 11: s = "downto expected"; break;
			case 12: s = "do expected"; break;
			case 13: s = "while expected"; break;
			case 14: s = "var expected"; break;
			case 15: s = "if expected"; break;
			case 16: s = "else expected"; break;
			case 17: s = "and expected"; break;
			case 18: s = "or expected"; break;
			case 19: s = "xor expected"; break;
			case 20: s = "bool expected"; break;
			case 21: s = "int expected"; break;
			case 22: s = "double expected"; break;
			case 23: s = "string expected"; break;
			case 24: s = "void expected"; break;
			case 25: s = "null expected"; break;
			case 26: s = "false expected"; break;
			case 27: s = "true expected"; break;
			case 28: s = "return expected"; break;
			case 29: s = "assgn expected"; break;
			case 30: s = "colon expected"; break;
			case 31: s = "comma expected"; break;
			case 32: s = "dot expected"; break;
			case 33: s = "eq expected"; break;
			case 34: s = "gt expected"; break;
			case 35: s = "gteq expected"; break;
			case 36: s = "lbrace expected"; break;
			case 37: s = "lbrack expected"; break;
			case 38: s = "lpar expected"; break;
			case 39: s = "lt expected"; break;
			case 40: s = "minus expected"; break;
			case 41: s = "not expected"; break;
			case 42: s = "plus expected"; break;
			case 43: s = "rbrace expected"; break;
			case 44: s = "rbrack expected"; break;
			case 45: s = "rpar expected"; break;
			case 46: s = "scolon expected"; break;
			case 47: s = "times expected"; break;
			case 48: s = "\"!=\" expected"; break;
			case 49: s = "\"<=\" expected"; break;
			case 50: s = "\"/\" expected"; break;
			case 51: s = "\"%\" expected"; break;
			case 52: s = "??? expected"; break;
			case 53: s = "invalid Primitive"; break;
			case 54: s = "invalid Statement"; break;
			case 55: s = "invalid ForStatement"; break;
			case 56: s = "invalid UnaryExpr"; break;
			case 57: s = "invalid ConstantExpression"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}

}
