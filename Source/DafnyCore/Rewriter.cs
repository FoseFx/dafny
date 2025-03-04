// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny {
  /// <summary>
  /// A class that plugins should extend, in order to provide an extra Rewriter to the pipeline.
  ///
  /// If the plugin defines no PluginConfiguration, then Dafny will instantiate every sub-class
  /// of Rewriter from the plugin, providing them with an ErrorReporter in the constructor
  /// as the first and only argument.
  /// </summary>
  public abstract class IRewriter {
    /// <summary>
    /// Used to report errors and warnings, with positional information.
    /// </summary>
    protected readonly ErrorReporter Reporter;

    /// <summary>
    /// Constructor that accepts an ErrorReporter
    /// You can obtain an ErrorReporter two following ways:
    /// * Extend a PluginConfiguration class, and override the method GetRewriters(), whose first argument is an ErrorReporter
    /// * Have no PluginConfiguration  class, and an ErrorReporter will be provided to your class's constructor.
    /// 
    /// Then you can use the protected field "reporter" like the following:
    /// 
    ///     reporter.Error(MessageSource.Compiler, token, "[Your plugin] Your error message here");
    ///
    /// The token is usually obtained on expressions and statements in the field `tok`
    /// If you do not have access to them, use moduleDefinition.GetFirstTopLevelToken()
    /// </summary>
    /// <param name="reporter">The error reporter. Usually outputs automatically to IDE or command-line</param>
    internal IRewriter(ErrorReporter reporter) {
      Contract.Requires(reporter != null);
      this.Reporter = reporter;
    }

    /// <summary>
    /// Phase 1/8
    /// Override this method to obtain the initial program after parsing and built-in pre-resolvers.
    /// You can then report errors using reporter.Error (see above)
    /// </summary>
    /// <param name="program">The entire program</param>
    internal virtual void PreResolve(Program program) {
      Contract.Requires(program != null);
    }

    /// <summary>
    /// Phase 2/8
    /// Override this method to obtain a module definition after parsing and built-in pre-resolvers.
    /// You can then report errors using reporter.Error(MessageSource.Resolver, token, "message") (see above)
    /// This is a good place to perform AST rewritings, if necessary
    /// </summary>
    /// <param name="moduleDefinition">A module definition before is resolved</param>
    internal virtual void PreResolve(ModuleDefinition moduleDefinition) {
      Contract.Requires(moduleDefinition != null);
    }

    /// <summary>
    /// Phase 3/8
    /// Override this method to obtain a module definition after bare resolution, if no error were thrown.
    /// You can then report errors using reporter.Error (see above)
    /// We heavily discourage AST rewriting after this stage, as automatic type checking will not take place anymore.
    /// </summary>
    /// <param name="moduleDefinition">A module definition after it is resolved and type-checked</param>
    internal virtual void PostResolveIntermediate(ModuleDefinition moduleDefinition) {
      Contract.Requires(moduleDefinition != null);
    }

    /// <summary>
    /// Phase 4/8
    /// Override this method to obtain a module definition after the module
    /// has been cloned and re-resolved prior to compilation.
    /// You can then report errors using reporter.Error (see above)
    /// </summary>
    /// <param name="moduleDefinition">A module definition after it
    /// is cloned and re-resolved for compilation.</param>
    internal virtual void PostCompileCloneAndResolve(ModuleDefinition moduleDefinition) {
      Contract.Requires(moduleDefinition != null);
    }

    /// <summary>
    /// Phase 5/8
    /// Override this method to obtain the module definition after resolution and
    /// SCC/Cyclicity/Recursivity analysis.
    /// You can then report errors using reporter.Error (see above)
    /// </summary>
    /// <param name="moduleDefinition">A module definition after it
    /// is resolved, type-checked and SCC/Cyclicity/Recursivity have been performed</param>
    internal virtual void PostCyclicityResolve(ModuleDefinition moduleDefinition) {
      Contract.Requires(moduleDefinition != null);
    }

    /// <summary>
    /// Phase 6/8
    /// Override this method to obtain the module definition after the phase decreasesResolve
    /// You can then report errors using reporter.Error (see above)
    /// </summary>
    /// <param name="moduleDefinition">A module definition after it
    /// is resolved, type-checked and SCC/Cyclicity/Recursivity and decreasesResolve checks have been performed</param>
    internal virtual void PostDecreasesResolve(ModuleDefinition moduleDefinition) {
      Contract.Requires(moduleDefinition != null);
    }

    /// <summary>
    /// Phase 7/8
    /// Override this method to obtain a module definition after the entire resolution pipeline
    /// You can then report errors using reporter.Error (see above)
    /// </summary>
    /// <param name="moduleDefinition">A module definition after it
    /// is resolved, type-checked and SCC/Cyclicity/Recursivity have been performed</param>
    internal virtual void PostResolve(ModuleDefinition moduleDefinition) {
      Contract.Requires(moduleDefinition != null);
    }

    /// <summary>
    /// Phase 8/8
    /// Override this method to obtain the final program after the entire resolution pipeline
    /// after the individual PostResolve on every module
    /// You can then report errors using reporter.Error (see above)
    /// </summary>
    /// <param name="program">The entire program after it is fully resolved</param>
    internal virtual void PostResolve(Program program) {
      Contract.Requires(program != null);
    }
  }

  public class AutoGeneratedToken : TokenWrapper {
    public AutoGeneratedToken(IToken wrappedToken)
      : base(wrappedToken) {
      Contract.Requires(wrappedToken != null);
    }

    public static bool Is(IToken tok) {
      while (tok is TokenWrapper w) {
        if (w is AutoGeneratedToken) {
          return true;
        }
        tok = w.WrappedToken;
      }
      return false;
    }

    public static Expression WrapExpression(Expression expr) {
      return Expression.CreateParensExpression(new AutoGeneratedToken(expr.tok), expr);
    }
  }

  public class TriggerGeneratingRewriter : IRewriter {
    internal TriggerGeneratingRewriter(ErrorReporter reporter) : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PostCyclicityResolve(ModuleDefinition m) {
      var finder = new Triggers.QuantifierCollector(Reporter);

      foreach (var decl in ModuleDefinition.AllCallables(m.TopLevelDecls)) {
        finder.Visit(decl, null);
      }

      var triggersCollector = new Triggers.TriggersCollector(finder.exprsInOldContext);
      foreach (var quantifierCollection in finder.quantifierCollections) {
        quantifierCollection.ComputeTriggers(triggersCollector);
        quantifierCollection.CommitTriggers();
      }
    }
  }

  internal class QuantifierSplittingRewriter : IRewriter {
    internal QuantifierSplittingRewriter(ErrorReporter reporter) : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PostResolveIntermediate(ModuleDefinition m) {
      var splitter = new Triggers.QuantifierSplitter();
      foreach (var decl in ModuleDefinition.AllCallables(m.TopLevelDecls)) {
        splitter.Visit(decl);
      }
      splitter.Commit();
    }
  }

  // write out the quantifier for ForallStmt
  public class ForallStmtRewriter : IRewriter {
    public ForallStmtRewriter(ErrorReporter reporter) : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PostResolveIntermediate(ModuleDefinition m) {
      var forallvisiter = new ForAllStmtVisitor(Reporter);
      foreach (var decl in ModuleDefinition.AllCallables(m.TopLevelDecls)) {
        forallvisiter.Visit(decl, true);
        if (decl is ExtremeLemma) {
          var prefixLemma = ((ExtremeLemma)decl).PrefixLemma;
          if (prefixLemma != null) {
            forallvisiter.Visit(prefixLemma, true);
          }
        }
      }

    }

    internal class ForAllStmtVisitor : TopDownVisitor<bool> {
      readonly ErrorReporter reporter;
      public ForAllStmtVisitor(ErrorReporter reporter) {
        Contract.Requires(reporter != null);
        this.reporter = reporter;
      }
      protected override bool VisitOneStmt(Statement stmt, ref bool st) {
        if (stmt is ForallStmt && ((ForallStmt)stmt).CanConvert) {
          ForallStmt s = (ForallStmt)stmt;
          if (s.Kind == ForallStmt.BodyKind.Proof) {
            Expression term = s.Ens.Count != 0 ? s.Ens[0].E : Expression.CreateBoolLiteral(s.Tok, true);
            for (int i = 1; i < s.Ens.Count; i++) {
              term = new BinaryExpr(s.Tok, BinaryExpr.ResolvedOpcode.And, term, s.Ens[i].E);
            }
            List<Expression> exprList = new List<Expression>();
            ForallExpr expr = new ForallExpr(s.Tok, s.EndTok, s.BoundVars, s.Range, term, s.Attributes);
            expr.Type = Type.Bool; // resolve here
            expr.Bounds = s.Bounds;
            exprList.Add(expr);
            s.ForallExpressions = exprList;
          } else if (s.Kind == ForallStmt.BodyKind.Assign) {
            if (s.BoundVars.Count != 0) {
              var s0 = (AssignStmt)s.S0;
              if (s0.Rhs is ExprRhs) {
                List<Expression> exprList = new List<Expression>();
                Expression Fi = null;
                Func<Expression, Expression> lhsBuilder = null;
                var lhs = s0.Lhs.Resolved;
                var i = s.BoundVars[0];
                if (s.BoundVars.Count == 1) {
                  //var lhsContext = null;
                  // Detect the following cases:
                  //   0: forall i | R(i) { F(i).f := E(i); }
                  //   1: forall i | R(i) { A[F(i)] := E(i); }
                  //   2: forall i | R(i) { F(i)[N] := E(i); }
                  if (lhs is MemberSelectExpr) {
                    var ll = (MemberSelectExpr)lhs;
                    Fi = ll.Obj;
                    lhsBuilder = e => {
                      var l = new MemberSelectExpr(ll.tok, e, ll.MemberName);
                      l.Member = ll.Member;
                      l.TypeApplication_AtEnclosingClass = ll.TypeApplication_AtEnclosingClass;
                      l.TypeApplication_JustMember = ll.TypeApplication_JustMember;
                      l.Type = ll.Type;
                      return l;
                    };
                  } else if (lhs is SeqSelectExpr) {
                    var ll = (SeqSelectExpr)lhs;
                    Contract.Assert(ll.SelectOne);
                    if (!FreeVariablesUtil.ContainsFreeVariable(ll.Seq, false, i)) {
                      Fi = ll.E0;
                      lhsBuilder = e => { var l = new SeqSelectExpr(ll.tok, true, ll.Seq, e, null, ll.CloseParen); l.Type = ll.Type; return l; };
                    } else if (!FreeVariablesUtil.ContainsFreeVariable(ll.E0, false, i)) {
                      Fi = ll.Seq;
                      lhsBuilder = e => { var l = new SeqSelectExpr(ll.tok, true, e, ll.E0, null, ll.CloseParen); l.Type = ll.Type; return l; };
                    }
                  }
                }
                var rhs = ((ExprRhs)s0.Rhs).Expr;
                bool usedInversion = false;
                if (Fi != null) {
                  var j = new BoundVar(i.tok, i.Name + "#inv", Fi.Type);
                  var jj = Expression.CreateIdentExpr(j);
                  var jList = new List<BoundVar>() { j };
                  var range = Expression.CreateAnd(Resolver.GetImpliedTypeConstraint(i, i.Type), s.Range);
                  var vals = InvertExpression(i, j, range, Fi);
#if DEBUG_PRINT
          Console.WriteLine("DEBUG: Trying to invert:");
          Console.WriteLine("DEBUG:   " + Printer.ExprToString(s.Range) + " && " + j.Name + " == " + Printer.ExprToString(Fi));
          if (vals == null) {
            Console.WriteLine("DEBUG: Can't");
          } else {
            Console.WriteLine("DEBUG: The inverse is the disjunction of the following:");
            foreach (var val in vals) {
              Console.WriteLine("DEBUG:   " + Printer.ExprToString(val.Range) + " && " + Printer.ExprToString(val.FInverse) + " == " + i.Name);
            }
          }
#endif
                  if (vals != null) {
                    foreach (var val in vals) {
                      lhs = lhsBuilder(jj);
                      Attributes attributes = new Attributes("trigger", new List<Expression>() { lhs }, s.Attributes);
                      var newRhs = Substitute(rhs, i, val.FInverse);
                      var msg = string.Format("rewrite: forall {0}: {1} {2}| {3} {{ {4} := {5}; }}",
                        j.Name,
                        j.Type.ToString(),
                        Printer.AttributesToString(attributes),
                        Printer.ExprToString(val.Range),
                        Printer.ExprToString(lhs),
                        Printer.ExprToString(newRhs));
                      reporter.Info(MessageSource.Resolver, stmt.Tok, msg);

                      var expr = new ForallExpr(s.Tok, s.EndTok, jList, val.Range, new BinaryExpr(s.Tok, BinaryExpr.ResolvedOpcode.EqCommon, lhs, newRhs), attributes);
                      expr.Type = Type.Bool; //resolve here
                      exprList.Add(expr);
                    }
                    usedInversion = true;
                  }
                }
                if (!usedInversion) {
                  var expr = new ForallExpr(s.Tok, s.EndTok, s.BoundVars, s.Range, new BinaryExpr(s.Tok, BinaryExpr.ResolvedOpcode.EqCommon, lhs, rhs), s.Attributes);
                  expr.Type = Type.Bool; // resolve here
                  expr.Bounds = s.Bounds;
                  exprList.Add(expr);
                }
                s.ForallExpressions = exprList;
              }
            }
          } else if (s.Kind == ForallStmt.BodyKind.Call) {
            var s0 = (CallStmt)s.S0;
            var argsSubstMap = new Dictionary<IVariable, Expression>();  // maps formal arguments to actuals
            Contract.Assert(s0.Method.Ins.Count == s0.Args.Count);
            for (int i = 0; i < s0.Method.Ins.Count; i++) {
              argsSubstMap.Add(s0.Method.Ins[i], s0.Args[i]);
            }
            var substituter = new AlphaConvertingSubstituter(s0.Receiver, argsSubstMap, s0.MethodSelect.TypeArgumentSubstitutionsWithParents());
            // Strengthen the range of the "forall" statement with the precondition of the call, suitably substituted with the actual parameters.
            if (Attributes.Contains(s.Attributes, "_autorequires")) {
              var range = s.Range;
              foreach (var req in s0.Method.Req) {
                var p = substituter.Substitute(req.E);  // substitute the call's actuals for the method's formals
                range = Expression.CreateAnd(range, p);
              }
              s.Range = range;
            }
            // substitute the call's actuals for the method's formals
            Expression term = s0.Method.Ens.Count != 0 ? substituter.Substitute(s0.Method.Ens[0].E) : Expression.CreateBoolLiteral(s.Tok, true);
            for (int i = 1; i < s0.Method.Ens.Count; i++) {
              term = new BinaryExpr(s.Tok, BinaryExpr.ResolvedOpcode.And, term, substituter.Substitute(s0.Method.Ens[i].E));
            }
            List<Expression> exprList = new List<Expression>();
            ForallExpr expr = new ForallExpr(s.Tok, s.EndTok, s.BoundVars, s.Range, term, s.Attributes);
            expr.Type = Type.Bool; // resolve here
            expr.Bounds = s.Bounds;
            exprList.Add(expr);
            s.ForallExpressions = exprList;
          } else {
            Contract.Assert(false);  // unexpected kind
          }
        }
        return true;  //visit the sub-parts with the same "st"
      }

      internal class ForallStmtTranslationValues {
        public readonly Expression Range;
        public readonly Expression FInverse;
        public ForallStmtTranslationValues(Expression range, Expression fInverse) {
          Contract.Requires(range != null);
          Contract.Requires(fInverse != null);
          Range = range;
          FInverse = fInverse;
        }
        public ForallStmtTranslationValues Subst(IVariable j, Expression e) {
          Contract.Requires(j != null);
          Contract.Requires(e != null);
          Dictionary<TypeParameter, Type> typeMap = new Dictionary<TypeParameter, Type>();
          var substMap = new Dictionary<IVariable, Expression>();
          substMap.Add(j, e);
          Substituter sub = new Substituter(null, substMap, typeMap);
          var v = new ForallStmtTranslationValues(sub.Substitute(Range), sub.Substitute(FInverse));
          return v;
        }
      }

      /// <summary>
      /// Find piecewise inverse of F under R.  More precisely, find lists of expressions P and F-1
      /// such that
      ///     R(i) && j == F(i)
      /// holds iff the disjunction of the following predicates holds:
      ///     P_0(j) && F-1_0(j) == i
      ///     ...
      ///     P_{n-1}(j) && F-1_{n-1}(j) == i
      /// If no such disjunction is found, return null.
      /// If such a disjunction is found, return for each disjunct:
      ///     * The predicate P_k(j), which is an expression that may have free occurrences of j (but no free occurrences of i)
      ///     * The expression F-1_k(j), which also may have free occurrences of j but not of i
      /// </summary>
      private List<ForallStmtTranslationValues> InvertExpression(BoundVar i, BoundVar j, Expression R, Expression F) {
        Contract.Requires(i != null);
        Contract.Requires(j != null);
        Contract.Requires(R != null);
        Contract.Requires(F != null);
        var vals = new List<ForallStmtTranslationValues>(InvertExpressionIter(i, j, R, F));
        if (vals.Count == 0) {
          return null;
        } else {
          return vals;
        }
      }
      /// <summary>
      /// See InvertExpression.
      /// </summary>
      private IEnumerable<ForallStmtTranslationValues> InvertExpressionIter(BoundVar i, BoundVar j, Expression R, Expression F) {
        Contract.Requires(i != null);
        Contract.Requires(j != null);
        Contract.Requires(R != null);
        Contract.Requires(F != null);
        F = F.Resolved;
        if (!FreeVariablesUtil.ContainsFreeVariable(F, false, i)) {
          // We're looking at R(i) && j == K.
          // We cannot invert j == K, but if we're lucky, R(i) contains a conjunct i==G.
          Expression r = Expression.CreateBoolLiteral(R.tok, true);
          Expression G = null;
          foreach (var c in Expression.Conjuncts(R)) {
            if (G == null && c is BinaryExpr) {
              var bin = (BinaryExpr)c;
              if (BinaryExpr.IsEqualityOp(bin.ResolvedOp)) {
                var id = bin.E0.Resolved as IdentifierExpr;
                if (id != null && id.Var == i) {
                  G = bin.E1;
                  continue;
                }
                id = bin.E1.Resolved as IdentifierExpr;
                if (id != null && id.Var == i) {
                  G = bin.E0;
                  continue;
                }
              }
            }
            r = Expression.CreateAnd(r, c);
          }
          if (G != null) {
            var jIsK = Expression.CreateEq(Expression.CreateIdentExpr(j), F, j.Type);
            var rr = Substitute(r, i, G);
            yield return new ForallStmtTranslationValues(Expression.CreateAnd(rr, jIsK), G);
          }
        } else if (F is IdentifierExpr) {
          var e = (IdentifierExpr)F;
          if (e.Var == i) {
            // We're looking at R(i) && j == i, which is particularly easy to invert:  R(j) && j == i
            var jj = Expression.CreateIdentExpr(j);
            yield return new ForallStmtTranslationValues(Substitute(R, i, jj), jj);
          }
        } else if (F is BinaryExpr) {
          var bin = (BinaryExpr)F;
          if (bin.ResolvedOp == BinaryExpr.ResolvedOpcode.Add && (bin.E0.Type.IsIntegerType || bin.E0.Type.IsRealType)) {
            if (!FreeVariablesUtil.ContainsFreeVariable(bin.E1, false, i)) {
              // We're looking at:  R(i) && j == f(i) + K.
              // By a recursive call, we'll ask to invert:  R(i) && j' == f(i).
              // For each P_0(j') && f-1_0(j') == i we get back, we yield:
              // P_0(j - K) && f-1_0(j - K) == i
              var jMinusK = Expression.CreateSubtract(Expression.CreateIdentExpr(j), bin.E1);
              foreach (var val in InvertExpression(i, j, R, bin.E0)) {
                yield return val.Subst(j, jMinusK);
              }
            } else if (!FreeVariablesUtil.ContainsFreeVariable(bin.E0, false, i)) {
              // We're looking at:  R(i) && j == K + f(i)
              // Do as in previous case, but with operands reversed.
              var jMinusK = Expression.CreateSubtract(Expression.CreateIdentExpr(j), bin.E0);
              foreach (var val in InvertExpression(i, j, R, bin.E1)) {
                yield return val.Subst(j, jMinusK);
              }
            }
          } else if (bin.ResolvedOp == BinaryExpr.ResolvedOpcode.Sub && (bin.E0.Type.IsIntegerType || bin.E0.Type.IsRealType)) {
            if (!FreeVariablesUtil.ContainsFreeVariable(bin.E1, false, i)) {
              // We're looking at:  R(i) && j == f(i) - K
              // Recurse on f(i) and then replace j := j + K
              var jPlusK = Expression.CreateAdd(Expression.CreateIdentExpr(j), bin.E1);
              foreach (var val in InvertExpression(i, j, R, bin.E0)) {
                yield return val.Subst(j, jPlusK);
              }
            } else if (!FreeVariablesUtil.ContainsFreeVariable(bin.E0, false, i)) {
              // We're looking at:  R(i) && j == K - f(i)
              // Recurse on f(i) and then replace j := K - j
              var kMinusJ = Expression.CreateSubtract(bin.E0, Expression.CreateIdentExpr(j));
              foreach (var val in InvertExpression(i, j, R, bin.E1)) {
                yield return val.Subst(j, kMinusJ);
              }
            }
          }
        } else if (F is ITEExpr) {
          var ife = (ITEExpr)F;
          // We're looking at R(i) && j == if A(i) then B(i) else C(i), which is equivalent to the disjunction of:
          //   R(i) && A(i) && j == B(i)
          //   R(i) && !A(i) && j == C(i)
          // We recurse on each one, yielding the results
          var r = Expression.CreateAnd(R, ife.Test);
          var valsThen = InvertExpression(i, j, r, ife.Thn);
          if (valsThen != null) {
            r = Expression.CreateAnd(R, Expression.CreateNot(ife.tok, ife.Test));
            var valsElse = InvertExpression(i, j, r, ife.Els);
            if (valsElse != null) {
              foreach (var val in valsThen) { yield return val; }
              foreach (var val in valsElse) { yield return val; }
            }
          }
        }
      }

      Expression Substitute(Expression expr, IVariable v, Expression e) {
        Dictionary<IVariable, Expression/*!*/> substMap = new Dictionary<IVariable, Expression>();
        Dictionary<TypeParameter, Type> typeMap = new Dictionary<TypeParameter, Type>();
        substMap.Add(v, e);
        Substituter sub = new Substituter(null, substMap, typeMap);
        return sub.Substitute(expr);
      }
    }
  }

  /// <summary>
  /// AutoContracts is an experimental feature that will fill much of the dynamic-frames boilerplate
  /// into a class.  From the user's perspective, what needs to be done is simply:
  ///  - mark the class with {:autocontracts}
  ///  - declare a function (or predicate) called Valid()
  ///
  /// AutoContracts will then:
  ///
  /// Declare, unless there already exist members with these names:
  ///    ghost var Repr: set(object)
  ///    predicate Valid()
  ///
  /// For function/predicate Valid(), insert:
  ///    reads this, Repr
  ///    ensures Valid() ==> this in Repr
  /// Into body of Valid(), insert (at the beginning of the body):
  ///    this in Repr && null !in Repr
  /// and also insert, for every array-valued field A declared in the class:
  ///    (A != null ==> A in Repr) &&
  /// and for every field F of a class type T where T has a field called Repr, also insert:
  ///    (F != null ==> F in Repr && F.Repr SUBSET Repr && this !in Repr && F.Valid())
  /// Except, if A or F is declared with {:autocontracts false}, then the implication will not
  /// be added.
  ///
  /// For every constructor, add:
  ///    ensures Valid() && fresh(Repr)
  /// At the end of the body of the constructor, add:
  ///    Repr := {this};
  ///    if (A != null) { Repr := Repr + {A}; }
  ///    if (F != null) { Repr := Repr + {F} + F.Repr; }
  ///
  /// In all the following cases, no "modifies" clause or "reads" clause is added if the user
  /// has given one.
  ///
  /// For every non-static non-ghost method that is not a "simple query method",
  /// add:
  ///    requires Valid()
  ///    modifies Repr
  ///    ensures Valid() && fresh(Repr - old(Repr))
  /// At the end of the body of the method, add:
  ///    if (A != null && !(A in Repr)) { Repr := Repr + {A}; }
  ///    if (F != null && !(F in Repr && F.Repr SUBSET Repr)) { Repr := Repr + {F} + F.Repr; }
  /// For every non-static non-twostate method that is either ghost or is a "simple query method",
  /// add:
  ///    requires Valid()
  /// For every non-static twostate method, add:
  ///    requires old(Valid())
  ///
  /// For every non-"Valid" non-static function, add:
  ///    requires Valid()
  ///    reads Repr
  /// </summary>
  public class AutoContractsRewriter : IRewriter {
    readonly BuiltIns builtIns;
    public AutoContractsRewriter(ErrorReporter reporter, BuiltIns builtIns)
      : base(reporter) {
      Contract.Requires(reporter != null);
      Contract.Requires(builtIns != null);
      this.builtIns = builtIns;
    }

    internal override void PreResolve(ModuleDefinition m) {
      foreach (var d in m.TopLevelDecls) {
        bool sayYes = true;
        if (d is ClassDecl && Attributes.ContainsBool(d.Attributes, "autocontracts", ref sayYes) && sayYes) {
          ProcessClassPreResolve((ClassDecl)d);
        }
      }
    }

    void ProcessClassPreResolve(ClassDecl cl) {
      // Add:  ghost var Repr: set<object>
      // ...unless a field with that name is already present
      if (!cl.Members.Exists(member => member is Field && member.Name == "Repr")) {
        Type ty = new SetType(true, builtIns.ObjectQ());
        var repr = new Field(new AutoGeneratedToken(cl.tok), "Repr", true, ty, null);
        cl.Members.Add(repr);
        AddHoverText(cl.tok, "{0}", Printer.FieldToString(repr));
      }
      // Add:  predicate Valid()
      // ...unless an instance function with that name is already present
      if (!cl.Members.Exists(member => member is Function && member.Name == "Valid" && !member.IsStatic)) {
        var valid = new Predicate(cl.tok, "Valid", false, true, new List<TypeParameter>(), new List<Formal>(), null,
          new List<AttributedExpression>(), new List<FrameExpression>(), new List<AttributedExpression>(), new Specification<Expression>(new List<Expression>(), null),
          null, Predicate.BodyOriginKind.OriginalOrInherited, null, null, null, null);
        cl.Members.Add(valid);
        // It will be added to hover text later
      }

      foreach (var member in cl.Members) {
        bool sayYes = true;
        if (Attributes.ContainsBool(member.Attributes, "autocontracts", ref sayYes) && !sayYes) {
          // the user has excluded this member
          continue;
        }
        if (member.RefinementBase != null) {
          // member is inherited from a module where it was already processed
          continue;
        }
        IToken tok = new AutoGeneratedToken(member.tok);
        if (member is Function && member.Name == "Valid" && !member.IsStatic) {
          var valid = (Function)member;
          // reads this, Repr
          var r0 = new ThisExpr(tok);
          var r1 = new MemberSelectExpr(tok, new ImplicitThisExpr(tok), "Repr");
          valid.Reads.Add(new FrameExpression(tok, r0, null));
          valid.Reads.Add(new FrameExpression(tok, r1, null));
          // ensures Valid() ==> this in Repr
          var post = new BinaryExpr(tok, BinaryExpr.Opcode.Imp,
            new FunctionCallExpr(tok, "Valid", new ImplicitThisExpr(tok), tok, tok, new List<ActualBinding>()),
            new BinaryExpr(tok, BinaryExpr.Opcode.In,
              new ThisExpr(tok),
               new MemberSelectExpr(tok, new ImplicitThisExpr(tok), "Repr")));
          valid.Ens.Insert(0, new AttributedExpression(post));
          if (member.tok == cl.tok) {
            // We added this function above, so produce a hover text for the entire function signature
            AddHoverText(cl.tok, "{0}", Printer.FunctionSignatureToString(valid));
          } else {
            AddHoverText(member.tok, "reads {0}, {1}\nensures {2}", r0, r1, post);
          }
        } else if (member is Function && !member.IsStatic) {
          var f = (Function)member;
          // requires Valid()
          var valid = new FunctionCallExpr(tok, "Valid", new ImplicitThisExpr(tok), tok, tok, new List<ActualBinding>());
          f.Req.Insert(0, new AttributedExpression(valid));
          var format = "requires {0}";
          var repr = new MemberSelectExpr(tok, new ImplicitThisExpr(tok), "Repr");
          if (f.Reads.Count == 0) {
            // reads Repr
            f.Reads.Add(new FrameExpression(tok, repr, null));
            format += "\nreads {1}";
          }
          AddHoverText(member.tok, format, valid, repr);
        } else if (member is Constructor) {
          var ctor = (Constructor)member;
          // ensures Valid();
          var valid = new FunctionCallExpr(tok, "Valid", new ImplicitThisExpr(tok), tok, tok, new List<ActualBinding>());
          ctor.Ens.Insert(0, new AttributedExpression(valid));
          // ensures fresh(Repr);
          var freshness = new FreshExpr(tok,
            new MemberSelectExpr(tok, new ImplicitThisExpr(tok), "Repr"));
          ctor.Ens.Insert(1, new AttributedExpression(freshness));
          var m0 = new ThisExpr(tok);
          AddHoverText(member.tok, "modifies {0}\nensures {1} && {2}", m0, valid, freshness);
        }
      }
    }

    internal override void PostResolveIntermediate(ModuleDefinition m) {
      foreach (var d in m.TopLevelDecls) {
        bool sayYes = true;
        if (d is ClassDecl && Attributes.ContainsBool(d.Attributes, "autocontracts", ref sayYes) && sayYes) {
          ProcessClassPostResolve((ClassDecl)d);
        }
      }
    }

    void ProcessClassPostResolve(ClassDecl cl) {
      // Find all fields of a reference type, and make a note of whether or not the reference type has a Repr field.
      // Also, find the Repr field and the function Valid in class "cl"
      Field ReprField = null;
      Function Valid = null;
      var subobjects = new List<Tuple<Field, Field, Function>>();
      foreach (var member in cl.Members) {
        var field = member as Field;
        if (field != null) {
          bool sayYes = true;
          if (field.Name == "Repr") {
            ReprField = field;
          } else if (Attributes.ContainsBool(field.Attributes, "autocontracts", ref sayYes) && !sayYes) {
            // ignore this field
          } else if (field.Type.IsRefType) {
            var rcl = (ClassDecl)((UserDefinedType)field.Type.NormalizeExpand()).ResolvedClass;
            Field rRepr = null;
            Function rValid = null;
            foreach (var memb in rcl.Members) {
              if (memb is Field && memb.Name == "Repr") {
                var f = (Field)memb;
                var t = f.Type.AsSetType;
                if (t != null && t.Arg.IsObjectQ) {
                  rRepr = f;
                }
              } else if (memb is Function && memb.Name == "Valid" && !memb.IsStatic) {
                var f = (Function)memb;
                if (f.Formals.Count == 0 && f.ResultType.IsBoolType) {
                  rValid = f;
                }
              }
              if (rRepr != null && rValid != null) {
                break;
              }
            }
            subobjects.Add(new Tuple<Field, Field, Function>(field, rRepr, rValid));
          }
        } else if (member is Function && member.Name == "Valid" && !member.IsStatic) {
          var fn = (Function)member;
          if (fn.Formals.Count == 0 && fn.ResultType.IsBoolType) {
            Valid = fn;
          }
        }
      }
      Contract.Assert(ReprField != null);  // we expect there to be a "Repr" field, since we added one in PreResolve

      IToken clTok = new AutoGeneratedToken(cl.tok);
      Type ty = Resolver.GetThisType(clTok, cl);
      var self = new ThisExpr(clTok);
      self.Type = ty;
      var implicitSelf = new ImplicitThisExpr(clTok);
      implicitSelf.Type = ty;
      var Repr = new MemberSelectExpr(clTok, implicitSelf, ReprField);

      foreach (var member in cl.Members) {
        bool sayYes = true;
        if (Attributes.ContainsBool(member.Attributes, "autocontracts", ref sayYes) && !sayYes) {
          continue;
        }
        IToken tok = new AutoGeneratedToken(member.tok);
        if (member is Function && member.Name == "Valid" && !member.IsStatic) {
          var valid = (Function)member;
          var validConjuncts = new List<Expression>();
          if (valid.IsGhost && valid.ResultType.IsBoolType) {
            if (valid.RefinementBase == null) {
              var c0 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.InSet, self, Repr);  // this in Repr
              var c1 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.NotInSet, new LiteralExpr(tok) { Type = builtIns.ObjectQ() }, Repr);  // null !in Repr
              var c = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.And, c0, c1);
              validConjuncts.Add(c);
            }

            foreach (var ff in subobjects) {
              if (ff.Item1.RefinementBase != null) {
                // the field has been inherited from a refined module, so don't include it here
                continue;
              }
              var F = new MemberSelectExpr(tok, implicitSelf, ff.Item1);
              var c0 = IsNotNull(tok, F);
              var c1 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.InSet, F, Repr);
              if (ff.Item2 == null) {
                // F != null ==> F in Repr  (so, nothing else to do)
              } else {
                // F != null ==> F in Repr && F.Repr <= Repr && this !in F.Repr && F.Valid()
                var FRepr = new MemberSelectExpr(tok, F, ff.Item2);
                var c2 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.Subset, FRepr, Repr);
                var c3 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.NotInSet, self, FRepr);
                c1 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.And, c1,
                  BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.And, c2, c3));
                if (ff.Item3 != null) {
                  // F.Valid()
                  c1 = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.And, c1,
                    ValidCall(tok, F, ff.Item3, valid));
                }
              }
              validConjuncts.Add(Expression.CreateImplies(c0, c1));
            }

            var hoverText = "";
            var sep = "";
            if (valid.Body == null) {
              valid.Body = Expression.CreateBoolLiteral(tok, true);
              if (validConjuncts.Count == 0) {
                hoverText = "true";
                sep = "\n";
              }
            }
            for (int i = validConjuncts.Count; 0 <= --i;) {
              var c = validConjuncts[i];
              valid.Body = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.And, c, valid.Body);
              hoverText = Printer.ExprToString(c) + sep + hoverText;
              sep = "\n";
            }
            AddHoverText(valid.tok, "{0}", hoverText);
          }

        } else if (member is Constructor) {
          var ctor = (Constructor)member;
          if (ctor.Body != null) {
            var sbs = (DividedBlockStmt)ctor.Body;
            var n = sbs.Body.Count;
            if (ctor.RefinementBase == null) {
              // Repr := {this};
              var e = new SetDisplayExpr(tok, true, new List<Expression>() { self });
              e.Type = new SetType(true, builtIns.ObjectQ());
              Statement s = new AssignStmt(tok, tok, Repr, new ExprRhs(e));
              s.IsGhost = true;
              sbs.AppendStmt(s);
            }
            AddSubobjectReprs(tok, ctor.BodyEndTok, subobjects, sbs, n, implicitSelf, Repr);
          }

        } else if (member is Method && !member.IsStatic && Valid != null) {
          var m = (Method)member;
          var addStatementsToUpdateRepr = false;
          if (member.IsGhost || IsSimpleQueryMethod(m)) {
            if (m.RefinementBase == null) {
              // requires Valid()
              var valid = ValidCall(tok, implicitSelf, Valid, m);
              if (m is TwoStateLemma) {
                // Instead use:  requires old(Valid())
                valid = new OldExpr(tok, valid);
                valid.Type = Type.Bool;
              }
              m.Req.Insert(0, new AttributedExpression(valid));
              AddHoverText(member.tok, "requires {0}", valid);
            }
          } else if (m.RefinementBase == null) {
            // requires Valid()
            var valid = ValidCall(tok, implicitSelf, Valid, m);
            m.Req.Insert(0, new AttributedExpression(valid));
            var format = "requires {0}";
            if (m.Mod.Expressions.Count == 0) {
              // modifies Repr
              m.Mod.Expressions.Add(new FrameExpression(Repr.tok, Repr, null));
              format += "\nmodifies {1}";
              addStatementsToUpdateRepr = true;
            }
            // ensures Valid()
            m.Ens.Insert(0, new AttributedExpression(valid));
            // ensures fresh(Repr - old(Repr));
            var e0 = new OldExpr(tok, Repr);
            e0.Type = Repr.Type;
            var e1 = new BinaryExpr(tok, BinaryExpr.Opcode.Sub, Repr, e0);
            e1.ResolvedOp = BinaryExpr.ResolvedOpcode.SetDifference;
            e1.Type = Repr.Type;
            var freshness = new FreshExpr(tok, e1);
            freshness.Type = Type.Bool;
            m.Ens.Insert(1, new AttributedExpression(freshness));
            AddHoverText(m.tok, format + "\nensures {0} && {2}", valid, Repr, freshness);
          } else {
            addStatementsToUpdateRepr = true;
          }

          if (addStatementsToUpdateRepr && m.Body != null) {
            var methodBody = (BlockStmt)m.Body;
            AddSubobjectReprs(tok, methodBody.EndTok, subobjects, methodBody, methodBody.Body.Count, implicitSelf, Repr);
          }
        }
      }
    }

    void AddSubobjectReprs(IToken tok, IToken endCurlyTok, List<Tuple<Field, Field, Function>> subobjects, BlockStmt block, int hoverTextFromHere,
      Expression implicitSelf, Expression Repr) {
      Contract.Requires(tok != null);
      Contract.Requires(endCurlyTok != null);
      Contract.Requires(subobjects != null);
      Contract.Requires(block != null);
      Contract.Requires(0 <= hoverTextFromHere && hoverTextFromHere <= block.Body.Count);
      Contract.Requires(implicitSelf != null);
      Contract.Requires(Repr != null);
      // TODO: these assignments should be included on every return path

      foreach (var ff in subobjects) {
        var F = new MemberSelectExpr(tok, implicitSelf, ff.Item1);  // create a resolved MemberSelectExpr
        Expression e = new SetDisplayExpr(tok, true, new List<Expression>() { F }) {
          Type = new SetType(true, builtIns.ObjectQ())  // resolve here
        };
        var rhs = new BinaryExpr(tok, BinaryExpr.Opcode.Add, Repr, e) {
          ResolvedOp = BinaryExpr.ResolvedOpcode.Union,
          Type = Repr.Type
        };
        Expression nguard = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.InSet, F, Repr);  // F in Repr
        if (ff.Item2 == null) {
          // Repr := Repr + {F}  (so, nothing else to do)
        } else {
          // Repr := Repr + {F} + F.Repr
          var FRepr = new MemberSelectExpr(tok, F, ff.Item2);  // create resolved MemberSelectExpr
          rhs = new BinaryExpr(tok, BinaryExpr.Opcode.Add, rhs, FRepr) {
            ResolvedOp = BinaryExpr.ResolvedOpcode.Union,
            Type = Repr.Type
          };
          var ng = BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.Subset, FRepr, Repr);  // F.Repr <= Repr
          nguard = Expression.CreateAnd(nguard, ng);
        }
        // Repr := Repr + ...;
        Statement s = new AssignStmt(tok, tok, Repr, new ExprRhs(rhs));
        s.IsGhost = true;
        // wrap if statement around s
        e = Expression.CreateAnd(IsNotNull(tok, F), Expression.CreateNot(tok, nguard));
        var thn = new BlockStmt(tok, tok, new List<Statement>() { s });
        thn.IsGhost = true;
        s = new IfStmt(tok, tok, false, e, thn, null);
        s.IsGhost = true;
        // finally, add s to the block
        block.AppendStmt(s);
      }
      if (hoverTextFromHere != block.Body.Count) {
        var hoverText = "";
        var sep = "";
        for (int i = hoverTextFromHere; i < block.Body.Count; i++) {
          hoverText += sep + Printer.StatementToString(block.Body[i]);
          sep = "\n";
        }
        AddHoverText(endCurlyTok, "{0}", hoverText);
      }
    }

    /// <summary>
    /// Returns an expression denoting "expr != null".  If the type
    /// of "expr" already implies "expr" is non-null, then an expression
    /// denoting "true" is returned.
    /// </summary>
    Expression IsNotNull(IToken tok, Expression expr) {
      Contract.Requires(tok != null);
      Contract.Requires(expr != null);
      if (expr.Type.IsNonNullRefType) {
        return Expression.CreateBoolLiteral(tok, true);
      } else {
        var cNull = new LiteralExpr(tok);
        cNull.Type = expr.Type;
        return BinBoolExpr(tok, BinaryExpr.ResolvedOpcode.NeqCommon, expr, cNull);
      }
    }

    bool IsSimpleQueryMethod(Method m) {
      // A simple query method has out parameters, its body has no effect other than to assign to them,
      // and the postcondition does not explicitly mention the pre-state.
      return m.Outs.Count != 0 && m.Body != null && LocalAssignsOnly(m.Body) &&
        m.Ens.TrueForAll(mfe => !MentionsOldState(mfe.E));
    }

    bool LocalAssignsOnly(Statement s) {
      Contract.Requires(s != null);
      if (s is AssignStmt) {
        var ss = (AssignStmt)s;
        return ss.Lhs.Resolved is IdentifierExpr;
      } else if (s is ConcreteUpdateStatement) {
        var ss = (ConcreteUpdateStatement)s;
        return ss.Lhss.TrueForAll(e => e.Resolved is IdentifierExpr);
      } else if (s is CallStmt) {
        return false;
      } else {
        foreach (var ss in s.SubStatements) {
          if (!LocalAssignsOnly(ss)) {
            return false;
          }
        }
      }
      return true;
    }

    /// <summary>
    /// Returns true iff 'expr' is a two-state expression, that is, if it mentions "old/fresh/unchanged(...)".
    /// </summary>
    static bool MentionsOldState(Expression expr) {
      Contract.Requires(expr != null);
      if (expr is OldExpr || expr is UnchangedExpr || expr is FreshExpr) {
        return true;
      }
      foreach (var ee in expr.SubExpressions) {
        if (MentionsOldState(ee)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Returns a resolved expression of the form "receiver.Valid()"
    /// </summary>
    public static Expression ValidCall(IToken tok, Expression receiver, Function Valid, ICallable callingContext) {
      Contract.Requires(tok != null);
      Contract.Requires(receiver != null);
      Contract.Requires(Valid != null);
      Contract.Requires(callingContext != null);
      Contract.Requires(receiver.Type.NormalizeExpand() is UserDefinedType && ((UserDefinedType)receiver.Type.NormalizeExpand()).ResolvedClass == Valid.EnclosingClass);
      Contract.Requires(receiver.Type.NormalizeExpand().TypeArgs.Count == Valid.EnclosingClass.TypeArgs.Count);
      var call = new FunctionCallExpr(tok, Valid.Name, receiver, tok, tok, new List<Expression>());
      call.Function = Valid;
      call.Type = Type.Bool;
      call.TypeApplication_AtEnclosingClass = receiver.Type.TypeArgs;
      call.TypeApplication_JustFunction = new List<Type>();
      callingContext.EnclosingModule.CallGraph.AddEdge((ICallable)CodeContextWrapper.Unwrap(callingContext), Valid);
      return call;
    }

    public static BinaryExpr BinBoolExpr(IToken tok, BinaryExpr.ResolvedOpcode rop, Expression e0, Expression e1) {
      var p = new BinaryExpr(tok, BinaryExpr.ResolvedOp2SyntacticOp(rop), e0, e1);
      p.ResolvedOp = rop;  // resolve here
      p.Type = Type.Bool;  // resolve here
      return p;
    }

    void AddHoverText(IToken tok, string format, params object[] args) {
      Contract.Requires(tok != null);
      Contract.Requires(format != null);
      Contract.Requires(args != null);
      for (int i = 0; i < args.Length; i++) {
        if (args[i] is Expression) {
          args[i] = Printer.ExprToString((Expression)args[i]);
        }
      }
      var s = "autocontracts:\n" + string.Format(format, args);
      Reporter.Info(MessageSource.Rewriter, tok, s.Replace("\n", "\n  "));
    }
  }

  /// <summary>
  /// For any function foo() with the :opaque attribute,
  /// hide the body, so that it can only be seen within its
  /// recursive clique (if any), or if the programmer
  /// specifically asks to see it via the reveal_foo() lemma
  /// </summary>
  public class OpaqueMemberRewriter : IRewriter {
    protected Dictionary<Method, Function> revealOriginal; // Map reveal_* lemmas (or two-state lemmas) back to their original functions

    public OpaqueMemberRewriter(ErrorReporter reporter)
      : base(reporter) {
      Contract.Requires(reporter != null);

      revealOriginal = new Dictionary<Method, Function>();
    }

    internal override void PreResolve(ModuleDefinition m) {
      foreach (var d in m.TopLevelDecls) {
        if (d is TopLevelDeclWithMembers) {
          ProcessOpaqueClassMembers((TopLevelDeclWithMembers)d);
        }
      }
    }

    internal override void PostResolveIntermediate(ModuleDefinition m) {
      foreach (var decl in ModuleDefinition.AllCallables(m.TopLevelDecls)) {
        if (decl is Lemma or TwoStateLemma) {
          var lem = (Method)decl;
          if (revealOriginal.ContainsKey(lem)) {
            var fn = revealOriginal[lem];
            AnnotateRevealFunction(lem, fn);
          }
        }
      }
    }

    protected void AnnotateRevealFunction(Method lemma, Function f) {
      Contract.Requires(lemma is Lemma || lemma is TwoStateLemma);
      Expression receiver;
      if (f.IsStatic) {
        receiver = new StaticReceiverExpr(f.tok, (TopLevelDeclWithMembers)f.EnclosingClass, true);
      } else {
        receiver = new ImplicitThisExpr(f.tok);
        //receiver.Type = GetThisType(expr.tok, (TopLevelDeclWithMembers)member.EnclosingClass);  // resolve here
      }
      var typeApplication = new List<Type>();
      var typeApplication_JustForMember = new List<Type>();
      for (int i = 0; i < f.TypeArgs.Count; i++) {
        // doesn't matter what type, just so we have it to make the resolver happy when resolving function member of
        // the fuel attribute. This might not be needed after fixing codeplex issue #172.
        typeApplication.Add(new IntType());
        typeApplication_JustForMember.Add(new IntType());
      }
      var nameSegment = new NameSegment(f.tok, f.Name, f.TypeArgs.Count == 0 ? null : typeApplication);
      var rr = new MemberSelectExpr(f.tok, receiver, f.Name);
      rr.Member = f;
      rr.TypeApplication_AtEnclosingClass = typeApplication;
      rr.TypeApplication_JustMember = typeApplication_JustForMember;
      List<Type> args = new List<Type>();
      for (int i = 0; i < f.Formals.Count; i++) {
        args.Add(new IntType());
      }
      rr.Type = new ArrowType(f.tok, args, new IntType());
      nameSegment.ResolvedExpression = rr;
      nameSegment.Type = rr.Type;
      LiteralExpr low = new LiteralExpr(f.tok, 1);
      LiteralExpr hi = new LiteralExpr(f.tok, 2);
      lemma.Attributes = new Attributes("fuel", new List<Expression>() { nameSegment, low, hi }, lemma.Attributes);
    }


    // Tells the function to use 0 fuel by default
    protected void ProcessOpaqueClassMembers(TopLevelDeclWithMembers c) {
      Contract.Requires(c != null);
      var newDecls = new List<MemberDecl>();
      foreach (var member in c.Members.Where(member => member is Function or ConstantField)) {
        if (!Attributes.Contains(member.Attributes, "opaque")) {
          // Nothing to do
        } else if (!RefinementToken.IsInherited(member.tok, c.EnclosingModuleDefinition)) {
          GenerateRevealLemma(member, newDecls);
        }
      }
      c.Members.AddRange(newDecls);
    }

    private void GenerateRevealLemma(MemberDecl m, List<MemberDecl> newDecls) {
      if (m is Function f) {
        // mark the opaque function with {:fuel 0, 0}
        var amount = new LiteralExpr(m.tok, 0);
        m.Attributes = new Attributes("fuel", new List<Expression>() { amount, amount }, m.Attributes);

        // That is, given:
        //   function {:opaque} foo(x:int, y:int) : int
        //     requires 0 <= x < 5;
        //     requires 0 <= y < 5;
        //     ensures foo(x, y) < 10;
        //   { x + y }
        // We produce:
        //   lemma {:axiom} {:auto_generated} {:fuel foo, 1, 2 } reveal_foo()
        //
        // If "foo" is a two-state function, then "reveal_foo" will be declared as a two-state lemma.
        //
        // The translator, in AddMethod, then adds ensures clauses to bump up the fuel parameters appropriately

        var cloner = new Cloner();

        List<TypeParameter> typeVars = new List<TypeParameter>();
        List<Type> optTypeArgs = new List<Type>();
        foreach (var tp in f.TypeArgs) {
          typeVars.Add(cloner.CloneTypeParam(tp));
          // doesn't matter what type, just so we have it to make the resolver happy when resolving function member of
          // the fuel attribute. This might not be needed after fixing codeplex issue #172.
          optTypeArgs.Add(new IntType());
        }
      }

      // Given:
      //   const {:opaque} foo := x
      // We produce:
      //   lemma {:auto_generated} {:opaque_reveal} {:verify false} reveal_foo()
      //     ensures foo == x

      // Add an axiom attribute so that the compiler won't complain about the lemma's lack of a body
      Attributes lemma_attrs = null;
      if (m is Function) {
        lemma_attrs = new Attributes("axiom", new List<Expression>(), lemma_attrs);
      }
      lemma_attrs = new Attributes("auto_generated", new List<Expression>(), lemma_attrs);
      lemma_attrs = new Attributes("opaque_reveal", new List<Expression>(), lemma_attrs);
      lemma_attrs = new Attributes("verify", new List<Expression>() { new LiteralExpr(m.tok, false) }, lemma_attrs);
      var ens = new List<AttributedExpression>();
      if (m is ConstantField c && c.Rhs != null) {
        ens.Add(new AttributedExpression(new BinaryExpr(c.tok, BinaryExpr.Opcode.Eq, new NameSegment(c.Tok, c.Name, null), c.Rhs)));
      }
      Method reveal;
      if (m is TwoStateFunction) {
        reveal = new TwoStateLemma(m.tok, "reveal_" + m.Name, m.HasStaticKeyword, new List<TypeParameter>(), new List<Formal>(), new List<Formal>(), new List<AttributedExpression>(),
          new Specification<FrameExpression>(new List<FrameExpression>(), null), ens,
          new Specification<Expression>(new List<Expression>(), null), null, lemma_attrs, null);
      } else {
        reveal = new Lemma(m.tok, "reveal_" + m.Name, m.HasStaticKeyword, new List<TypeParameter>(), new List<Formal>(), new List<Formal>(), new List<AttributedExpression>(),
          new Specification<FrameExpression>(new List<FrameExpression>(), null), ens,
          new Specification<Expression>(new List<Expression>(), null), null, lemma_attrs, null);
      }
      newDecls.Add(reveal);
      reveal.InheritVisibility(m, true);
      if (m is Function fn) {
        revealOriginal[reveal] = fn;
      }
    }
  }


  /// <summary>
  /// Automatically accumulate requires for function calls within a function body,
  /// if requested via {:autoreq}
  /// </summary>
  public class AutoReqFunctionRewriter : IRewriter {
    Function parentFunction;
    bool containsMatch; // TODO: Track this per-requirement, rather than per-function

    public AutoReqFunctionRewriter(ErrorReporter reporter)
      : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PostResolveIntermediate(ModuleDefinition m) {
      var components = m.CallGraph.TopologicallySortedComponents();

      foreach (var scComponent in components) {  // Visit the call graph bottom up, so anything we call already has its prequisites calculated
        if (scComponent is Function) {
          Function fn = (Function)scComponent;
          if (Attributes.ContainsBoolAtAnyLevel(fn, "autoReq")) {
            parentFunction = fn;  // Remember where the recursion started
            containsMatch = false;  // Assume no match statements are involved

            List<AttributedExpression> auto_reqs = new List<AttributedExpression>();

            // First handle all of the requirements' preconditions
            foreach (AttributedExpression req in fn.Req) {
              foreach (Expression e in generateAutoReqs(req.E)) {
                auto_reqs.Add(CreateAutoAttributedExpression(e, req.Attributes));
              }
            }
            fn.Req.InsertRange(0, auto_reqs); // Need to come before the actual requires
            addAutoReqToolTipInfoToFunction("pre", fn, auto_reqs);

            // Then the body itself, if any
            if (fn.Body != null) {
              auto_reqs = new List<AttributedExpression>();
              foreach (Expression e in generateAutoReqs(fn.Body)) {
                auto_reqs.Add(CreateAutoAttributedExpression(e));
              }
              fn.Req.AddRange(auto_reqs);
              addAutoReqToolTipInfoToFunction("post", fn, auto_reqs);
            }
          }
        } else if (scComponent is Method) {
          Method method = (Method)scComponent;
          if (Attributes.ContainsBoolAtAnyLevel(method, "autoReq")) {
            parentFunction = null;
            containsMatch = false; // Assume no match statements are involved

            List<AttributedExpression> auto_reqs = new List<AttributedExpression>();
            foreach (AttributedExpression req in method.Req) {
              List<Expression> local_auto_reqs = generateAutoReqs(req.E);
              foreach (Expression local_auto_req in local_auto_reqs) {
                auto_reqs.Add(CreateAutoAttributedExpression(local_auto_req));
              }
            }
            method.Req.InsertRange(0, auto_reqs); // Need to come before the actual requires
            addAutoReqToolTipInfoToMethod("pre", method, auto_reqs);
          }
        }
      }
    }

    public void addAutoReqToolTipInfoToFunction(string label, Function f, List<AttributedExpression> reqs) {
      string prefix = "auto requires " + label + " ";
      string tip = "";

      string sep = "";
      foreach (var req in reqs) {
        if (containsMatch) {  // Pretty print the requirements
          tip += sep + prefix + Printer.ExtendedExprToString(req.E) + ";";
        } else {
          tip += sep + prefix + Printer.ExprToString(req.E) + ";";
        }
        sep = "\n";
      }

      if (!tip.Equals("")) {
        Reporter.Info(MessageSource.Rewriter, f.tok, tip);
        if (DafnyOptions.O.AutoReqPrintFile != null) {
          using (System.IO.TextWriter writer = new System.IO.StreamWriter(DafnyOptions.O.AutoReqPrintFile, true)) {
            writer.WriteLine(f.Name);
            writer.WriteLine("\t" + tip);
          }
        }
      }
    }

    public void addAutoReqToolTipInfoToMethod(string label, Method method, List<AttributedExpression> reqs) {
      string tip = "";

      foreach (var req in reqs) {
        string prefix = "auto ";
        prefix += " requires " + label + " ";
        if (containsMatch) {  // Pretty print the requirements
          tip += prefix + Printer.ExtendedExprToString(req.E) + ";\n";
        } else {
          tip += prefix + Printer.ExprToString(req.E) + ";\n";
        }
      }

      if (!tip.Equals("")) {
        Reporter.Info(MessageSource.Rewriter, method.tok, tip);
        if (DafnyOptions.O.AutoReqPrintFile != null) {
          using (System.IO.TextWriter writer = new System.IO.StreamWriter(DafnyOptions.O.AutoReqPrintFile, true)) {
            writer.WriteLine(method.Name);
            writer.WriteLine("\t" + tip);
          }
        }
      }
    }

    // Stitch a list of expressions together with logical ands
    Expression andify(IToken tok, List<Expression> exprs) {
      Expression ret = Expression.CreateBoolLiteral(new AutoGeneratedToken(tok), true);

      foreach (var expr in exprs) {
        ret = Expression.CreateAnd(ret, expr);
      }

      return ret;
    }

    static AttributedExpression CreateAutoAttributedExpression(Expression e, Attributes attrs = null) {
      return new AttributedExpression(AutoGeneratedToken.WrapExpression(e), attrs);
    }

    List<Expression> gatherReqs(Function f, List<Expression> args, Expression f_this) {
      List<Expression> translated_f_reqs = new List<Expression>();

      if (f.Req.Count > 0) {
        Dictionary<IVariable, Expression/*!*/> substMap = new Dictionary<IVariable, Expression>();
        Dictionary<TypeParameter, Type> typeMap = new Dictionary<TypeParameter, Type>();

        for (int i = 0; i < f.Formals.Count; i++) {
          substMap.Add(f.Formals[i], args[i]);
        }

        foreach (var req in f.Req) {
          Substituter sub = new Substituter(f_this, substMap, typeMap);
          translated_f_reqs.Add(sub.Substitute(req.E));
        }
      }

      return translated_f_reqs;
    }

    List<Expression> generateAutoReqs(Expression expr) {
      List<Expression> reqs = new List<Expression>();

      if (expr is LiteralExpr) {
      } else if (expr is ThisExpr) {
      } else if (expr is IdentifierExpr) {
      } else if (expr is SetDisplayExpr) {
        SetDisplayExpr e = (SetDisplayExpr)expr;

        foreach (var elt in e.Elements) {
          reqs.AddRange(generateAutoReqs(elt));
        }
      } else if (expr is MultiSetDisplayExpr) {
        MultiSetDisplayExpr e = (MultiSetDisplayExpr)expr;
        foreach (var elt in e.Elements) {
          reqs.AddRange(generateAutoReqs(elt));
        }
      } else if (expr is SeqDisplayExpr) {
        SeqDisplayExpr e = (SeqDisplayExpr)expr;
        foreach (var elt in e.Elements) {
          reqs.AddRange(generateAutoReqs(elt));
        }
      } else if (expr is MapDisplayExpr) {
        MapDisplayExpr e = (MapDisplayExpr)expr;

        foreach (ExpressionPair p in e.Elements) {
          reqs.AddRange(generateAutoReqs(p.A));
          reqs.AddRange(generateAutoReqs(p.B));
        }
      } else if (expr is MemberSelectExpr) {
        MemberSelectExpr e = (MemberSelectExpr)expr;
        Contract.Assert(e.Member != null && e.Member is Field);

        reqs.AddRange(generateAutoReqs(e.Obj));
      } else if (expr is SeqSelectExpr) {
        SeqSelectExpr e = (SeqSelectExpr)expr;

        reqs.AddRange(generateAutoReqs(e.Seq));
        if (e.E0 != null) {
          reqs.AddRange(generateAutoReqs(e.E0));
        }

        if (e.E1 != null) {
          reqs.AddRange(generateAutoReqs(e.E1));
        }
      } else if (expr is SeqUpdateExpr) {
        SeqUpdateExpr e = (SeqUpdateExpr)expr;
        reqs.AddRange(generateAutoReqs(e.Seq));
        reqs.AddRange(generateAutoReqs(e.Index));
        reqs.AddRange(generateAutoReqs(e.Value));
      } else if (expr is DatatypeUpdateExpr) {
        foreach (var ee in expr.SubExpressions) {
          reqs.AddRange(generateAutoReqs(ee));
        }
      } else if (expr is FunctionCallExpr) {
        FunctionCallExpr e = (FunctionCallExpr)expr;

        // All of the arguments need to be satisfied
        foreach (var arg in e.Args) {
          reqs.AddRange(generateAutoReqs(arg));
        }

        if (parentFunction != null && ModuleDefinition.InSameSCC(e.Function, parentFunction)) {
          // We're making a call within the same SCC, so don't descend into this function
        } else {
          reqs.AddRange(gatherReqs(e.Function, e.Args, e.Receiver));
        }
      } else if (expr is DatatypeValue) {
        DatatypeValue dtv = (DatatypeValue)expr;
        Contract.Assert(dtv.Ctor != null);  // since dtv has been successfully resolved
        for (int i = 0; i < dtv.Arguments.Count; i++) {
          Expression arg = dtv.Arguments[i];
          reqs.AddRange(generateAutoReqs(arg));
        }
      } else if (expr is OldExpr) {
      } else if (expr is MatchExpr) {
        MatchExpr e = (MatchExpr)expr;
        containsMatch = true;
        reqs.AddRange(generateAutoReqs(e.Source));

        List<MatchCaseExpr> newMatches = new List<MatchCaseExpr>();
        foreach (MatchCaseExpr caseExpr in e.Cases) {
          //MatchCaseExpr c = new MatchCaseExpr(caseExpr.tok, caseExpr.Id, caseExpr.Arguments, andify(caseExpr.tok, generateAutoReqs(caseExpr.Body)));
          //c.Ctor = caseExpr.Ctor; // resolve here
          MatchCaseExpr c = Expression.CreateMatchCase(caseExpr, andify(caseExpr.tok, generateAutoReqs(caseExpr.Body)));
          newMatches.Add(c);
        }

        reqs.Add(Expression.CreateMatch(e.tok, e.Source, newMatches, e.Type));
      } else if (expr is SeqConstructionExpr) {
        var e = (SeqConstructionExpr)expr;
        reqs.AddRange(generateAutoReqs(e.N));
        reqs.AddRange(generateAutoReqs(e.Initializer));
      } else if (expr is MultiSetFormingExpr) {
        MultiSetFormingExpr e = (MultiSetFormingExpr)expr;
        reqs.AddRange(generateAutoReqs(e.E));
      } else if (expr is UnaryExpr) {
        UnaryExpr e = (UnaryExpr)expr;
        Expression arg = e.E;
        reqs.AddRange(generateAutoReqs(arg));
      } else if (expr is BinaryExpr) {
        BinaryExpr e = (BinaryExpr)expr;

        switch (e.ResolvedOp) {
          case BinaryExpr.ResolvedOpcode.Imp:
          case BinaryExpr.ResolvedOpcode.And:
            reqs.AddRange(generateAutoReqs(e.E0));
            foreach (var req in generateAutoReqs(e.E1)) {
              // We only care about this req if E0 is true, since And short-circuits
              reqs.Add(Expression.CreateImplies(e.E0, req));
            }
            break;

          case BinaryExpr.ResolvedOpcode.Or:
            reqs.AddRange(generateAutoReqs(e.E0));
            foreach (var req in generateAutoReqs(e.E1)) {
              // We only care about this req if E0 is false, since Or short-circuits
              reqs.Add(Expression.CreateImplies(Expression.CreateNot(e.E1.tok, e.E0), req));
            }
            break;

          default:
            reqs.AddRange(generateAutoReqs(e.E0));
            reqs.AddRange(generateAutoReqs(e.E1));
            break;
        }
      } else if (expr is TernaryExpr) {
        var e = (TernaryExpr)expr;

        reqs.AddRange(generateAutoReqs(e.E0));
        reqs.AddRange(generateAutoReqs(e.E1));
        reqs.AddRange(generateAutoReqs(e.E2));
      } else if (expr is LetExpr) {
        var e = (LetExpr)expr;

        if (e.Exact) {
          foreach (var rhs in e.RHSs) {
            reqs.AddRange(generateAutoReqs(rhs));
          }
          var new_reqs = generateAutoReqs(e.Body);
          if (new_reqs.Count > 0) {
            reqs.Add(Expression.CreateLet(e.tok, e.LHSs, e.RHSs, andify(e.tok, new_reqs), e.Exact));
          }
        } else {
          // TODO: Still need to figure out what the right choice is here:
          // Given: var x :| g(x); f(x, y) do we:
          //    1) Update the original statement to be: var x :| g(x) && WP(f(x,y)); f(x, y)
          //    2) Add forall x :: g(x) ==> WP(f(x, y)) to the function's requirements
          //    3) Current option -- do nothing.  Up to the spec writer to fix
        }
      } else if (expr is QuantifierExpr) {
        QuantifierExpr e = (QuantifierExpr)expr;

        // See LetExpr for issues with the e.Range

        var auto_reqs = generateAutoReqs(e.Term);
        if (auto_reqs.Count > 0) {
          Expression allReqsSatisfied = andify(e.Term.tok, auto_reqs);
          Expression allReqsSatisfiedAndTerm = Expression.CreateAnd(allReqsSatisfied, e.Term);
          e.UpdateTerm(allReqsSatisfiedAndTerm);
          Reporter.Info(MessageSource.Rewriter, e.tok, "autoreq added (" + Printer.ExtendedExprToString(allReqsSatisfied) + ") &&");
        }
      } else if (expr is SetComprehension) {
        var e = (SetComprehension)expr;
        // Translate "set xs | R :: T"

        // See LetExpr for issues with the e.Range
        //reqs.AddRange(generateAutoReqs(e.Range));
        var auto_reqs = generateAutoReqs(e.Term);
        if (auto_reqs.Count > 0) {
          reqs.Add(Expression.CreateQuantifier(new ForallExpr(e.tok, e.BodyEndTok, e.BoundVars, e.Range, andify(e.Term.tok, auto_reqs), e.Attributes), true));
        }
      } else if (expr is MapComprehension) {
        var e = (MapComprehension)expr;
        // Translate "map x | R :: T" into
        // See LetExpr for issues with the e.Range
        //reqs.AddRange(generateAutoReqs(e.Range));
        var auto_reqs = new List<Expression>();
        if (e.TermLeft != null) {
          auto_reqs.AddRange(generateAutoReqs(e.TermLeft));
        }
        auto_reqs.AddRange(generateAutoReqs(e.Term));
        if (auto_reqs.Count > 0) {
          reqs.Add(Expression.CreateQuantifier(new ForallExpr(e.tok, e.BodyEndTok, e.BoundVars, e.Range, andify(e.Term.tok, auto_reqs), e.Attributes), true));
        }
      } else if (expr is StmtExpr) {
        var e = (StmtExpr)expr;
        reqs.AddRange(generateAutoReqs(e.E));
      } else if (expr is ITEExpr) {
        ITEExpr e = (ITEExpr)expr;
        reqs.AddRange(generateAutoReqs(e.Test));
        reqs.Add(Expression.CreateITE(e.Test, andify(e.Thn.tok, generateAutoReqs(e.Thn)), andify(e.Els.tok, generateAutoReqs(e.Els))));
      } else if (expr is NestedMatchExpr) {
        // Generate autoReq on e.ResolvedExpression, but also on the unresolved body in case something (e.g. another cloner) clears the resolved expression
        var e = (NestedMatchExpr)expr;

        var autoReqs = generateAutoReqs(e.ResolvedExpression);
        var newMatch = new NestedMatchExpr(e.tok, e.Source, e.Cases, e.UsesOptionalBraces);
        newMatch.ResolvedExpression = andify(e.tok, autoReqs);
        newMatch.Type = newMatch.ResolvedExpression.Type;
        reqs.Add(newMatch);
      } else if (expr is ConcreteSyntaxExpression) {
        var e = (ConcreteSyntaxExpression)expr;
        reqs.AddRange(generateAutoReqs(e.ResolvedExpression));
      } else {
        //Contract.Assert(false); throw new cce.UnreachableException();  // unexpected expression
      }

      return reqs;
    }
  }

  public class ProvideRevealAllRewriter : IRewriter {
    public ProvideRevealAllRewriter(ErrorReporter reporter)
      : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PreResolve(ModuleDefinition m) {
      var declarations = m.TopLevelDecls;

      foreach (var d in declarations) {
        if (d is ModuleExportDecl me) {
          var revealAll = me.RevealAll || DafnyOptions.O.DisableScopes;

          HashSet<string> explicitlyRevealedTopLevelIDs = null;
          if (!revealAll) {
            explicitlyRevealedTopLevelIDs = new HashSet<string>();
            foreach (var esig in me.Exports) {
              if (esig.ClassId == null && !esig.Opaque) {
                explicitlyRevealedTopLevelIDs.Add(esig.Id);
              }
            }
          }

          if (revealAll || me.ProvideAll) {
            foreach (var newt in declarations) {
              if (!newt.CanBeExported()) {
                continue;
              }

              if (!(newt is DefaultClassDecl)) {
                me.Exports.Add(new ExportSignature(newt.tok, newt.Name, !revealAll || !newt.CanBeRevealed()));
              }

              if (newt is TopLevelDeclWithMembers) {
                var cl = (TopLevelDeclWithMembers)newt;
                var newtIsRevealed = revealAll || explicitlyRevealedTopLevelIDs.Contains(newt.Name);

                foreach (var mem in cl.Members) {
                  var opaque = !revealAll || !mem.CanBeRevealed();
                  if (newt is DefaultClassDecl) {
                    // add everything from the default class
                    me.Exports.Add(new ExportSignature(mem.tok, mem.Name, opaque));
                  } else if (mem is Constructor && !newtIsRevealed) {
                    // "provides *" does not pick up class constructors, unless the class is to be revealed
                  } else if (opaque && mem is Field field && !(mem is ConstantField) && !newtIsRevealed) {
                    // "provides *" does not pick up mutable fields, unless the class is to be revealed
                  } else {
                    me.Exports.Add(new ExportSignature(cl.tok, cl.Name, mem.tok, mem.Name, opaque));
                  }
                }
              }
            }
          }
        }
      }
    }
  }




  /// <summary>
  /// Replace all occurrences of attribute {:timeLimitMultiplier X} with {:timeLimit Y}
  /// where Y = X*default-time-limit or Y = X*command-line-time-limit
  /// </summary>
  public class TimeLimitRewriter : IRewriter {
    public TimeLimitRewriter(ErrorReporter reporter)
      : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PreResolve(ModuleDefinition m) {
      foreach (var d in m.TopLevelDecls) {
        if (d is TopLevelDeclWithMembers tld) {
          foreach (MemberDecl member in tld.Members) {
            if (member is Function || member is Method) {
              // Check for the timeLimitMultiplier attribute
              if (Attributes.Contains(member.Attributes, "timeLimitMultiplier")) {
                Attributes attrs = member.Attributes;
                foreach (var attr in attrs.AsEnumerable()) {
                  if (attr.Name == "timeLimitMultiplier") {
                    if (attr.Args.Count == 1 && attr.Args[0] is LiteralExpr) {
                      var arg = attr.Args[0] as LiteralExpr;
                      System.Numerics.BigInteger value = (System.Numerics.BigInteger)arg.Value;
                      if (value.Sign > 0) {
                        uint current_limit = 0;
                        string name = "";
                        if (DafnyOptions.O.ResourceLimit > 0) {
                          // Interpret this as multiplying the resource limit
                          current_limit = DafnyOptions.O.ResourceLimit;
                          name = "rlimit";
                        } else {
                          // Interpret this as multiplying the time limit
                          current_limit = DafnyOptions.O.TimeLimit > 0 ? DafnyOptions.O.TimeLimit : 10;  // Default to 10 seconds
                          name = "timeLimit";
                        }
                        Expression newArg = new LiteralExpr(attr.Args[0].tok, value * current_limit);
                        member.Attributes = new Attributes("_" + name, new List<Expression>() { newArg }, attrs);
                        if (Attributes.Contains(attrs, name)) {
                          Reporter.Warning(MessageSource.Rewriter, member.tok, "timeLimitMultiplier annotation overrides " + name + " annotation");
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }


  class MatchCaseExprSubstituteCloner : Cloner {
    private List<Tuple<CasePattern<BoundVar>, BoundVar>> patternSubst;
    private BoundVar oldvar;
    private BoundVar var;

    // the cloner is called after resolving the body of matchexpr, trying
    // to replace casepattern in the body that has been replaced by bv
    public MatchCaseExprSubstituteCloner(List<Tuple<CasePattern<BoundVar>, BoundVar>> subst) {
      this.patternSubst = subst;
      this.oldvar = null;
      this.var = null;
    }

    public MatchCaseExprSubstituteCloner(BoundVar oldvar, BoundVar var) {
      this.patternSubst = null;
      this.oldvar = oldvar;
      this.var = var;
    }

    public override BoundVar CloneBoundVar(BoundVar bv) {
      // replace bv with this.var if bv == oldvar
      BoundVar bvNew;
      if (oldvar != null && bv.Name.Equals(oldvar.Name)) {
        bvNew = new BoundVar(new AutoGeneratedToken(bv.tok), var.Name, CloneType(bv.Type));
      } else {
        bvNew = new BoundVar(Tok(bv.tok), bv.Name, CloneType(bv.Type));
      }
      bvNew.IsGhost = bv.IsGhost;
      return bvNew;
    }

    public override NameSegment CloneNameSegment(Expression expr) {
      var e = (NameSegment)expr;
      if (oldvar != null && e.Name.Equals(oldvar.Name)) {
        return new NameSegment(new AutoGeneratedToken(e.tok), var.Name, e.OptTypeArguments == null ? null : e.OptTypeArguments.ConvertAll(CloneType));
      } else {
        return new NameSegment(Tok(e.tok), e.Name, e.OptTypeArguments == null ? null : e.OptTypeArguments.ConvertAll(CloneType));
      }
    }

    public override Expression CloneApplySuffix(ApplySuffix e) {
      // if the ApplySuffix matches the CasePattern, then replace it with the BoundVar.
      CasePattern<BoundVar> cp = null;
      BoundVar bv = null;
      if (FindMatchingPattern(e, out cp, out bv)) {
        if (bv.tok is MatchCaseToken) {
          Contract.Assert(e.Args.Count == cp.Arguments.Count);
          for (int i = 0; i < e.Args.Count; i++) {
            ((MatchCaseToken)bv.tok).AddVar(e.Args[i].tok, cp.Arguments[i].Var, false);
          }
        }
        return new NameSegment(new AutoGeneratedToken(e.tok), bv.Name, null);
      } else {
        return new ApplySuffix(Tok(e.tok), e.AtTok == null ? null : Tok(e.AtTok), CloneExpr(e.Lhs), e.Bindings.ArgumentBindings.ConvertAll(CloneActualBinding), Tok(e.CloseParen));
      }
    }

    private bool FindMatchingPattern(ApplySuffix e, out CasePattern<BoundVar> pattern, out BoundVar bv) {
      pattern = null;
      bv = null;
      if (patternSubst == null) {
        return false;
      }
      Expression lhs = e.Lhs;
      if (!(lhs is NameSegment)) {
        return false;
      }
      string applyName = ((NameSegment)lhs).Name;
      foreach (Tuple<CasePattern<BoundVar>, BoundVar> pair in patternSubst) {
        var cp = pair.Item1;
        string ctorName = cp.Id;
        if (!(applyName.Equals(ctorName)) || (e.Args.Count != cp.Arguments.Count)) {
          continue;
        }
        bool found = true;
        for (int i = 0; i < e.Args.Count; i++) {
          var arg1 = e.Args[i];
          var arg2 = cp.Arguments[i];
          if (arg1.Resolved is IdentifierExpr) {
            var bv1 = ((IdentifierExpr)arg1.Resolved).Var;
            if (bv1 != arg2.Var) {
              found = false;
            }
          } else {
            found = false;
          }
        }
        if (found) {
          pattern = cp;
          bv = pair.Item2;
          return true;
        }
      }
      return false;
    }
  }

  // MatchCaseToken is used to record BoundVars that are consolidated due to rewrite of
  // nested match patterns. We want to record the original BoundVars that are consolidated
  // so that they will show up in the IDE correctly.
  public class MatchCaseToken : AutoGeneratedToken {
    public readonly List<Tuple<IToken, BoundVar, bool>> varList;
    public MatchCaseToken(IToken tok)
      : base(tok) {
      varList = new List<Tuple<IToken, BoundVar, bool>>();
    }

    public void AddVar(IToken tok, BoundVar var, bool isDefinition) {
      varList.Add(new Tuple<IToken, BoundVar, bool>(tok, var, isDefinition));
    }
  }

  // A cloner that replace the original token with AutoGeneratedToken.
  class AutoGeneratedTokenCloner : Cloner {
    public override IToken Tok(IToken tok) {
      return new AutoGeneratedToken(tok);
    }
  }

  // ===========================================================================================

  public class InductionRewriter : IRewriter {
    internal InductionRewriter(ErrorReporter reporter) : base(reporter) {
      Contract.Requires(reporter != null);
    }

    internal override void PostDecreasesResolve(ModuleDefinition m) {
      if (DafnyOptions.O.Induction == 0) {
        // Don't bother inferring :induction attributes.  This will also have the effect of not warning about malformed :induction attributes
      } else {
        foreach (var decl in m.TopLevelDecls) {
          if (decl is TopLevelDeclWithMembers) {
            var cl = (TopLevelDeclWithMembers)decl;
            foreach (var member in cl.Members) {
              if (member is ExtremeLemma) {
                var method = (ExtremeLemma)member;
                ProcessMethodExpressions(method);
                ComputeLemmaInduction(method.PrefixLemma);
                ProcessMethodExpressions(method.PrefixLemma);
              } else if (member is Method) {
                var method = (Method)member;
                ComputeLemmaInduction(method);
                ProcessMethodExpressions(method);
              } else if (member is ExtremePredicate) {
                var function = (ExtremePredicate)member;
                ProcessFunctionExpressions(function);
                ProcessFunctionExpressions(function.PrefixPredicate);
              } else if (member is Function) {
                var function = (Function)member;
                ProcessFunctionExpressions(function);
                if (function.ByMethodDecl != null) {
                  ProcessMethodExpressions(function.ByMethodDecl);
                }
              }
            }
          }
          if (decl is NewtypeDecl) {
            var nt = (NewtypeDecl)decl;
            if (nt.Constraint != null) {
              var visitor = new Induction_Visitor(this);
              visitor.Visit(nt.Constraint);
            }
          }
        }
      }
    }

    void ProcessMethodExpressions(Method method) {
      Contract.Requires(method != null);
      var visitor = new Induction_Visitor(this);
      method.Req.ForEach(mfe => visitor.Visit(mfe.E));
      method.Ens.ForEach(mfe => visitor.Visit(mfe.E));
      if (method.Body != null) {
        visitor.Visit(method.Body);
      }
    }

    void ProcessFunctionExpressions(Function function) {
      Contract.Requires(function != null);
      var visitor = new Induction_Visitor(this);
      function.Req.ForEach(visitor.Visit);
      function.Ens.ForEach(visitor.Visit);
      if (function.Body != null) {
        visitor.Visit(function.Body);
      }
    }

    void ComputeLemmaInduction(Method method) {
      Contract.Requires(method != null);
      if (method.Body != null && method.IsGhost && method.Mod.Expressions.Count == 0 && method.Outs.Count == 0 && !(method is ExtremeLemma)) {
        var specs = new List<Expression>();
        method.Req.ForEach(mfe => specs.Add(mfe.E));
        method.Ens.ForEach(mfe => specs.Add(mfe.E));
        ComputeInductionVariables(method.tok, method.Ins, specs, method, ref method.Attributes);
      }
    }

    void ComputeInductionVariables<VarType>(IToken tok, List<VarType> boundVars, List<Expression> searchExprs, Method lemma, ref Attributes attributes) where VarType : class, IVariable {
      Contract.Requires(tok != null);
      Contract.Requires(boundVars != null);
      Contract.Requires(searchExprs != null);
      Contract.Requires(DafnyOptions.O.Induction != 0);

      var args = Attributes.FindExpressions(attributes, "induction");  // we only look at the first one we find, since it overrides any other ones
      if (args == null) {
        if (DafnyOptions.O.Induction < 2) {
          // No explicit induction variables and we're asked not to infer anything, so we're done
          return;
        } else if (DafnyOptions.O.Induction == 2 && lemma != null) {
          // We're asked to infer induction variables only for quantifiers, not for lemmas
          return;
        } else if (DafnyOptions.O.Induction == 4 && lemma == null) {
          // We're asked to infer induction variables only for lemmas, not for quantifiers
          return;
        }
        // GO INFER below (only select boundVars)
      } else if (args.Count == 0) {
        // {:induction} is treated the same as {:induction true}, which says to automatically infer induction variables
        // GO INFER below (all boundVars)
      } else if (args.Count == 1 && args[0] is LiteralExpr && ((LiteralExpr)args[0]).Value is bool) {
        // {:induction false} or {:induction true}
        if (!(bool)((LiteralExpr)args[0]).Value) {
          // we're told not to infer anything
          return;
        }
        // GO INFER below (all boundVars)
      } else {
        // Here, we're expecting the arguments to {:induction args} to be a sublist of "this;boundVars", where "this" is allowed only
        // if "lemma" denotes an instance lemma.
        var goodArguments = new List<Expression>();
        var i = lemma != null && !lemma.IsStatic ? -1 : 0;  // -1 says it's okay to see "this" or any other parameter; 0 <= i says it's okay to see parameter i or higher
        foreach (var arg in args) {
          var ie = arg.Resolved as IdentifierExpr;
          if (ie != null) {
            var j = boundVars.FindIndex(v => v == ie.Var);
            if (0 <= j && i <= j) {
              goodArguments.Add(ie);
              i = j + 1;
              continue;
            }
            if (0 <= j) {
              Reporter.Warning(MessageSource.Rewriter, arg.tok, "{0}s given as :induction arguments must be given in the same order as in the {1}; ignoring attribute",
                lemma != null ? "lemma parameter" : "bound variable", lemma != null ? "lemma" : "quantifier");
              return;
            }
            // fall through for j < 0
          } else if (lemma != null && arg.Resolved is ThisExpr) {
            if (i < 0) {
              goodArguments.Add(arg.Resolved);
              i = 0;
              continue;
            }
            Reporter.Warning(MessageSource.Rewriter, arg.tok, "lemma parameters given as :induction arguments must be given in the same order as in the lemma; ignoring attribute");
            return;
          }
          Reporter.Warning(MessageSource.Rewriter, arg.tok, "invalid :induction attribute argument; expected {0}{1}; ignoring attribute",
            i == 0 ? "'false' or 'true' or " : "",
            lemma != null ? "lemma parameter" : "bound variable");
          return;
        }
        // The argument list was legal, so let's use it for the _induction attribute
        attributes = new Attributes("_induction", goodArguments, attributes);
        return;
      }

      // Okay, here we go, coming up with good induction setting for the given situation
      var inductionVariables = new List<Expression>();
      if (lemma != null && !lemma.IsStatic) {
        if (args != null || searchExprs.Exists(expr => FreeVariablesUtil.ContainsFreeVariable(expr, true, null))) {
          inductionVariables.Add(new ThisExpr(lemma));
        }
      }
      foreach (IVariable n in boundVars) {
        if (!(n.Type.IsTypeParameter || n.Type.IsOpaqueType || n.Type.IsInternalTypeSynonym) && (args != null || searchExprs.Exists(expr => VarOccursInArgumentToRecursiveFunction(expr, n)))) {
          inductionVariables.Add(new IdentifierExpr(n.Tok, n));
        }
      }
      if (inductionVariables.Count != 0) {
        // We found something usable, so let's record that in an attribute
        attributes = new Attributes("_induction", inductionVariables, attributes);
        // And since we're inferring something, let's also report that in a hover text.
        var s = Printer.OneAttributeToString(attributes, "induction");
        if (lemma is PrefixLemma) {
          s = lemma.Name + " " + s;
        }
        Reporter.Info(MessageSource.Rewriter, tok, s);
      }
    }
    class Induction_Visitor : BottomUpVisitor {
      readonly InductionRewriter IndRewriter;
      public Induction_Visitor(InductionRewriter inductionRewriter) {
        Contract.Requires(inductionRewriter != null);
        IndRewriter = inductionRewriter;
      }
      protected override void VisitOneExpr(Expression expr) {
        var q = expr as QuantifierExpr;
        if (q != null && q.SplitQuantifier == null) {
          IndRewriter.ComputeInductionVariables(q.tok, q.BoundVars, new List<Expression>() { q.LogicalBody() }, null, ref q.Attributes);
        }
      }
    }

    /// <summary>
    /// Returns 'true' iff by looking at 'expr' the Induction Heuristic determines that induction should be applied to 'n'.
    /// More precisely:
    ///   DafnyInductionHeuristic      Return 'true'
    ///   -----------------------      -------------
    ///        0                       always
    ///        1    if 'n' occurs as   any subexpression (of 'expr')
    ///        2    if 'n' occurs as   any subexpression of          any index argument of an array/sequence select expression or any                       argument to a recursive function
    ///        3    if 'n' occurs as   a prominent subexpression of  any index argument of an array/sequence select expression or any                       argument to a recursive function
    ///        4    if 'n' occurs as   any subexpression of                                                                       any                       argument to a recursive function
    ///        5    if 'n' occurs as   a prominent subexpression of                                                               any                       argument to a recursive function
    ///        6    if 'n' occurs as   a prominent subexpression of                                                               any decreases-influencing argument to a recursive function
    /// </summary>
    public static bool VarOccursInArgumentToRecursiveFunction(Expression expr, IVariable n) {
      switch (DafnyOptions.O.InductionHeuristic) {
        case 0: return true;
        case 1: return FreeVariablesUtil.ContainsFreeVariable(expr, false, n);
        default: return VarOccursInArgumentToRecursiveFunction(expr, n, false);
      }
    }

    /// <summary>
    /// Worker routine for VarOccursInArgumentToRecursiveFunction(expr,n), where the additional parameter 'exprIsProminent' says whether or
    /// not 'expr' has prominent status in its context.
    /// DafnyInductionHeuristic cases 0 and 1 are assumed to be handled elsewhere (i.e., a precondition of this method is DafnyInductionHeuristic is at least 2).
    /// </summary>
    static bool VarOccursInArgumentToRecursiveFunction(Expression expr, IVariable n, bool exprIsProminent) {
      Contract.Requires(expr != null);
      Contract.Requires(n != null);

      // The following variable is what gets passed down to recursive calls if the subexpression does not itself acquire prominent status.
      var subExprIsProminent = DafnyOptions.O.InductionHeuristic == 2 || DafnyOptions.O.InductionHeuristic == 4 ? /*once prominent, always prominent*/exprIsProminent : /*reset the prominent status*/false;

      if (expr is IdentifierExpr) {
        var e = (IdentifierExpr)expr;
        return exprIsProminent && e.Var == n;
      } else if (expr is SeqSelectExpr) {
        var e = (SeqSelectExpr)expr;
        var q = DafnyOptions.O.InductionHeuristic < 4 || subExprIsProminent;
        return VarOccursInArgumentToRecursiveFunction(e.Seq, n, subExprIsProminent) ||  // this subexpression does not acquire "prominent" status
          (e.E0 != null && VarOccursInArgumentToRecursiveFunction(e.E0, n, q)) ||  // this one does (unless arrays/sequences are excluded)
          (e.E1 != null && VarOccursInArgumentToRecursiveFunction(e.E1, n, q));    // ditto
      } else if (expr is MultiSelectExpr) {
        var e = (MultiSelectExpr)expr;
        var q = DafnyOptions.O.InductionHeuristic < 4 || subExprIsProminent;
        return VarOccursInArgumentToRecursiveFunction(e.Array, n, subExprIsProminent) ||
          e.Indices.Exists(exp => VarOccursInArgumentToRecursiveFunction(exp, n, q));
      } else if (expr is FunctionCallExpr) {
        var e = (FunctionCallExpr)expr;
        // For recursive functions:  arguments are "prominent"
        // For non-recursive function:  arguments are "prominent" if the call is
        var rec = e.Function.IsRecursive && e.CoCall != FunctionCallExpr.CoCallResolution.Yes;
        var decr = e.Function.Decreases.Expressions;
        bool variantArgument;
        if (DafnyOptions.O.InductionHeuristic < 6) {
          variantArgument = rec;
        } else {
          // The receiver is considered to be "variant" if the function is recursive and the receiver participates
          // in the effective decreases clause of the function.  The receiver participates if it's a free variable
          // of a term in the explicit decreases clause.
          variantArgument = rec && decr.Exists(ee => FreeVariablesUtil.ContainsFreeVariable(ee, true, null));
        }
        if (VarOccursInArgumentToRecursiveFunction(e.Receiver, n, variantArgument || subExprIsProminent)) {
          return true;
        }
        Contract.Assert(e.Function.Formals.Count == e.Args.Count);
        for (int i = 0; i < e.Function.Formals.Count; i++) {
          var f = e.Function.Formals[i];
          var exp = e.Args[i];
          if (DafnyOptions.O.InductionHeuristic < 6) {
            variantArgument = rec;
          } else if (rec) {
            // The argument position is considered to be "variant" if the function is recursive and...
            // ... it has something to do with why the callee is well-founded, which happens when...
            if (f is ImplicitFormal) {
              // ... it is the argument is the implicit _k parameter, which is always first in the effective decreases clause of a prefix lemma, or
              variantArgument = true;
            } else if (decr.Exists(ee => FreeVariablesUtil.ContainsFreeVariable(ee, false, f))) {
              // ... it participates in the effective decreases clause of the function, which happens when it is
              // a free variable of a term in the explicit decreases clause, or
              variantArgument = true;
            } else {
              // ... the callee is a prefix predicate.
              variantArgument = true;
            }
          }
          if (VarOccursInArgumentToRecursiveFunction(exp, n, variantArgument || subExprIsProminent)) {
            return true;
          }
        }
        return false;
      } else if (expr is TernaryExpr) {
        var e = (TernaryExpr)expr;
        switch (e.Op) {
          case TernaryExpr.Opcode.PrefixEqOp:
          case TernaryExpr.Opcode.PrefixNeqOp:
            return VarOccursInArgumentToRecursiveFunction(e.E0, n, true) ||
              VarOccursInArgumentToRecursiveFunction(e.E1, n, subExprIsProminent) ||
              VarOccursInArgumentToRecursiveFunction(e.E2, n, subExprIsProminent);
          default:
            Contract.Assert(false); throw new cce.UnreachableException();  // unexpected ternary expression
        }
      } else if (expr is DatatypeValue) {
        var e = (DatatypeValue)expr;
        var q = n.Type.IsDatatype ? exprIsProminent : subExprIsProminent;  // prominent status continues, if we're looking for a variable whose type is a datatype
        return e.Arguments.Exists(exp => VarOccursInArgumentToRecursiveFunction(exp, n, q));
      } else if (expr is UnaryExpr) {
        var e = (UnaryExpr)expr;
        // both Not and SeqLength preserve prominence
        return VarOccursInArgumentToRecursiveFunction(e.E, n, exprIsProminent);
      } else if (expr is BinaryExpr) {
        var e = (BinaryExpr)expr;
        bool q;
        switch (e.ResolvedOp) {
          case BinaryExpr.ResolvedOpcode.Add:
          case BinaryExpr.ResolvedOpcode.Sub:
          case BinaryExpr.ResolvedOpcode.Mul:
          case BinaryExpr.ResolvedOpcode.Div:
          case BinaryExpr.ResolvedOpcode.Mod:
          case BinaryExpr.ResolvedOpcode.LeftShift:
          case BinaryExpr.ResolvedOpcode.RightShift:
          case BinaryExpr.ResolvedOpcode.BitwiseAnd:
          case BinaryExpr.ResolvedOpcode.BitwiseOr:
          case BinaryExpr.ResolvedOpcode.BitwiseXor:
          case BinaryExpr.ResolvedOpcode.Union:
          case BinaryExpr.ResolvedOpcode.Intersection:
          case BinaryExpr.ResolvedOpcode.SetDifference:
          case BinaryExpr.ResolvedOpcode.Concat:
            // these operators preserve prominence
            q = exprIsProminent;
            break;
          default:
            // whereas all other binary operators do not
            q = subExprIsProminent;
            break;
        }
        return VarOccursInArgumentToRecursiveFunction(e.E0, n, q) ||
          VarOccursInArgumentToRecursiveFunction(e.E1, n, q);
      } else if (expr is StmtExpr) {
        var e = (StmtExpr)expr;
        // ignore the statement
        return VarOccursInArgumentToRecursiveFunction(e.E, n);

      } else if (expr is ITEExpr) {
        var e = (ITEExpr)expr;
        return VarOccursInArgumentToRecursiveFunction(e.Test, n, subExprIsProminent) ||  // test is not "prominent"
          VarOccursInArgumentToRecursiveFunction(e.Thn, n, exprIsProminent) ||  // but the two branches are
          VarOccursInArgumentToRecursiveFunction(e.Els, n, exprIsProminent);
      } else if (expr is OldExpr ||
                 expr is ConcreteSyntaxExpression ||
                 expr is BoxingCastExpr ||
                 expr is UnboxingCastExpr) {
        foreach (var exp in expr.SubExpressions) {
          if (VarOccursInArgumentToRecursiveFunction(exp, n, exprIsProminent)) {  // maintain prominence
            return true;
          }
        }
        return false;
      } else {
        // in all other cases, reset the prominence status and recurse on the subexpressions
        foreach (var exp in expr.SubExpressions) {
          if (VarOccursInArgumentToRecursiveFunction(exp, n, subExprIsProminent)) {
            return true;
          }
        }
        return false;
      }
    }
  }

  class PluginRewriter : IRewriter {
    private Plugins.Rewriter internalRewriter;

    public PluginRewriter(ErrorReporter reporter, Plugins.Rewriter internalRewriter) : base(reporter) {
      this.internalRewriter = internalRewriter;
    }

    internal override void PostResolve(ModuleDefinition moduleDefinition) {
      internalRewriter.PostResolve(moduleDefinition);
    }

    internal override void PostResolve(Program program) {
      internalRewriter.PostResolve(program);
    }
  }
}
