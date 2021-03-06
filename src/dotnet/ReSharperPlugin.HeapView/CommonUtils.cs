using System;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace ReSharperPlugin.HeapView
{
  public static class CommonUtils
  {
    [Pure]
    public static bool IsStringConcatOperatorReference([CanBeNull] this IReference reference)
    {
      if (reference?.Resolve() is (ISignOperator { IsPredefined: true, Parameters: { Count: 2 } parameters }, _))
      {
        var lhsType = parameters[0].Type;
        var rhsType = parameters[1].Type;

        if (lhsType.IsString()) return rhsType.IsString() || rhsType.IsObject();
        if (rhsType.IsString()) return lhsType.IsString() || lhsType.IsObject();
      }

      return false;
    }

    [Pure]
    public static bool IsInTheContextWhereAllocationsAreNotImportant([NotNull] this ITreeNode context)
    {
      foreach (var containingNode in context.ContainingNodes(returnThis: true))
      {
        if (containingNode is IAttribute) return true; // compile-time boxing, array creations

        // throw arguments
        if (containingNode is IThrowExpression) return true;
        if (containingNode is IThrowStatement) return true;

        if (containingNode is ICSharpStatement statement)
        {
          return NextStatementsExecutionAlwaysEndsWithThrowStatement(statement);
        }
      }

      return false;
    }

    [Pure]
    private static bool NextStatementsExecutionAlwaysEndsWithThrowStatement([NotNull] ICSharpStatement statement)
    {
      var nextStatement = statement.GetNextStatement();
      while (nextStatement != null)
      {
        if (nextStatement is IThrowStatement) return true;
        if (HasControlFlowJumps(nextStatement)) return false;

        nextStatement = nextStatement.GetNextStatement();
      }

      return false;

      static bool HasControlFlowJumps(ICSharpStatement statement, bool allowContinue = false, bool allowBreak = false)
      {
        switch (statement)
        {
          case IBlock block:
            foreach (var blockStatement in block.StatementsEnumerable)
              if (HasControlFlowJumps(blockStatement, allowContinue, allowBreak))
                return true;

            return false;

          case ICheckedStatement checkedStatement:
            return HasControlFlowJumps(checkedStatement.Body, allowContinue, allowBreak);

          case IUncheckedStatement uncheckedStatement:
            return HasControlFlowJumps(uncheckedStatement.Body, allowContinue, allowBreak);

          case ILoopStatement loopStatement:
            return HasControlFlowJumps(loopStatement.Body, allowContinue: true, allowBreak: true);

          case IIfStatement ifStatement:
            return HasControlFlowJumps(ifStatement.Then, allowContinue, allowBreak)
                || HasControlFlowJumps(ifStatement.Else, allowContinue, allowBreak);

          case ILockStatement lockStatement:
            return HasControlFlowJumps(lockStatement.Body, allowContinue, allowBreak);

          case ISwitchStatement switchStatement:
            foreach (var switchSection in switchStatement.SectionsEnumerable)
            foreach (var switchSectionStatement in switchSection.StatementsEnumerable)
              if (IsControlFlowJumpStatement(switchSectionStatement, allowContinue, allowBreak: true))
                return true;

            return false;

          case ITryStatement tryStatement:
            if (HasControlFlowJumps(tryStatement.Try, allowContinue, allowBreak)) return true;

            foreach (var catchClause in tryStatement.CatchesEnumerable)
              if (HasControlFlowJumps(catchClause.Body, allowContinue, allowBreak))
                return true;

            return HasControlFlowJumps(tryStatement.FinallyBlock, allowContinue, allowBreak);

          case IUnsafeCodeFixedStatement fixedStatement:
            return HasControlFlowJumps(fixedStatement.Body, allowContinue, allowBreak);

          case IUnsafeCodeUnsafeStatement unsafeStatement:
            return HasControlFlowJumps(unsafeStatement.Body, allowContinue, allowBreak);

          case IUsingStatement usingStatement:
            return HasControlFlowJumps(usingStatement.Body, allowContinue, allowBreak);

          case { }:
            return IsControlFlowJumpStatement(statement, allowContinue, allowBreak);

          case null:
            return false;
        }
      }

      static bool IsControlFlowJumpStatement(ICSharpStatement statement, bool allowContinue, bool allowBreak)
      {
        switch (statement)
        {
          case IBreakStatement _ when !allowBreak:
          case IContinueStatement _ when !allowContinue:
          case IGotoCaseStatement _:
          case IGotoStatement _:
          case ILoopStatement _:
          case IReturnStatement _:
          case IYieldStatement _:
            return true;

          default:
            return false;
        }
      }
    }

    class B { }
    class C : B { }
    class D : B { }

    class Switch
    {
      void M(B b)
      {
        switch (b)
        {
          case C _:
            break;
        }
      }
    }
  }
}