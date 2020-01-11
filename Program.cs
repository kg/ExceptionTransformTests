using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Mono.Runtime.Internal;

namespace ExceptionTransformTests {
    public class CustomExceptionFilter : ExceptionFilter {
        public override int Evaluate (object _exc) {
            var exc = (Exception)_exc;
            Console.WriteLine($"CustomFilter.Evaluate({exc.Message})");
            return exception_execute_handler;
        }
    }

    public static class Program {
        public static void Main (string[] args) {
            Console.WriteLine("Start");
            NestedFilters("NestedFilters");
            NestedFilters("NestedFilters3");

            var customFilter = new CustomExceptionFilter();
            Mono.Runtime.Internal.ExceptionFilter.Push(customFilter);
            try {
                NestedFilters("RunCustomFilter");
            } catch (Exception exc) {
                Console.WriteLine($"CustomFilter result = {customFilter.Result}");
            } finally {
                Mono.Runtime.Internal.ExceptionFilter.Pop(customFilter);
            }

            CatchAndSilence();
            RunWithExceptionFilter();
            MultipleTypedCatches();
            NestedFiltersInOneFunction(7);

            var tempv = new TestGenericClass<int>();
            var ret = tempv.GenericInstanceMethodWithFilterAndIndirectReference(default(int), "test");
            Console.WriteLine("ret=" + ret);

            Console.WriteLine("Done executing");
            if (Debugger.IsAttached)
                Console.ReadLine();
        }

        static void PrintException (string message, Exception exc) {
            var ts = exc.ToString();
            var lines = ts.Replace(Environment.NewLine, "\n").Split('\n').Take(4);
            foreach (var line in lines)
                Console.WriteLine(line);
            Console.WriteLine();
        }

        static void MultipleTypedCatches () {
            try {
                throw new FieldAccessException();
            } catch (InvalidOperationException ioe) when (true) {
                Console.WriteLine("MultipleTypedCatches IOE");
            } catch (NullReferenceException nre) {
                Console.WriteLine("MultipleTypedCatches NRE");
            } catch {
                Console.WriteLine("MultipleTypedCatches catch");
            }
        }

        static bool FilterOn (Exception exc, string s) {
            Console.WriteLine($"FilterOn({s}) processing {exc.Message}");
            return exc.Message == s;
        }

        static void NestedFilters (string s) {
            try {
                Console.WriteLine($"== NestedFilters({s}) ==");
                NestedFilters2(s);
                Console.WriteLine("NestedFilters2 didn't throw?");
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters") || false) {
                Console.WriteLine($"NestedFilters caught {exc.Message} via filter");
            } catch {
                Console.WriteLine($"NestedFilters caught via fallback");
                throw;
            }
            Console.WriteLine("NestedFilters left try and catch");
        }

        static void NestedFilters2 (string s) {
            try {
                NestedFilters3(s);
                Console.WriteLine("NestedFilters3 didn't throw?");
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters2") || (exc.Message == "nope")) {
                Console.WriteLine($"NestedFilters2 caught {exc.Message} via filter");
            }
        }

        static void NestedFilters3 (string s) {
            try {
                throw new Exception(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters3")) {
                Console.WriteLine($"NestedFilters3 caught {exc.Message} via filter");
            }
        }

        static void Throws () {
            throw new Exception("Throws");
        }

        static void DoesNotThrow () {
            Console.WriteLine("DoesNotThrow");
        }

        static int ThrowsWithResult (int i) {
            throw new Exception("ThrowsWithResult");
            return i;
        }

        static void CatchAndRethrow () {
            try {
                DoesNotThrow();
                Throws();
            } catch (Exception exc) {
                PrintException("Catch and rethrow", exc);
                throw;
            }
        }

        static void ArithmeticWithAFilter (int x, int y) {
            int m = 0;
            try {
                var tmp = new int[x];
                for (int i = 0, j = 0; i < x; i++) {
                    tmp[i] = j;
                    j += y;
                    m = j;
                }
            } catch when (y == 7) {
                Console.WriteLine("m == " + m);
            }
        }

        static void MethodWithThreeFilters (int i) {
            float j = 0.5f;

            try {
                j = 1.0f;
                throw new Exception();
            } catch (NullReferenceException) when (i == 3) {
                Console.WriteLine("Caught NRE with i=3");
            } catch (ArgumentException) when (i == 2) {
                Console.WriteLine("Caught AE with i=2");
            } catch when (i == 1) {
                Console.WriteLine("Caught with i=1");
            } catch {
                Console.WriteLine("Caught fallback");
            }
        }

        static void MethodWithTwoSeparateCatchBlocks (int i) {
            int j = 0;
            Console.WriteLine("A");

            try {
                throw new Exception();
            } catch when (i == 3) {
                Console.WriteLine("a i==3");
            } catch {
                Console.WriteLine("a i!=3");
            }

            j++;

            Console.WriteLine("B");

            try {
                throw new Exception();
            } catch when (i == 2) {
                Console.WriteLine("b i==2");
            } catch {
                Console.WriteLine("b i!=2");
            }
        }

        static void CatchAndSilence () {
            try {
                CatchAndRethrow();
            } catch (Exception exc) {
                PrintException("Catch and silence", exc);
            }
        }

        static void RunWithExceptionFilter () {
            try {
                Console.WriteLine("Silence with filter");
                CatchAndSilence();
                Console.WriteLine("Rethrow with filter");
                CatchAndRethrow();
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                PrintException("Caught by filter", exc);
            } catch {
                Console.WriteLine("Caught without filter");
            }
        }

        static void NestedFiltersInOneFunction (int i) {
            object _expression = null;
            string value = null;
            long logScopeId = 0; // DataCommonEventSource.Log.EnterScope("<ds.DataColumn.set_Expression|API> {0}, '{1}'", ObjectID, value);
            object _table = null;

            if (value == null)
            {
                value = string.Empty;
            }

            try
            {
                object newExpression = null;
                if (value.Length > 0)
                {
                    object testExpression = new object();
                    if (true)
                        newExpression = testExpression;
                }

                if (_expression == null && newExpression != null)
                {
                    if (false)
                    {
                        throw new Exception();
                    }

                    // We need to make sure the column is not involved in any Constriants
                    if (_table != null)
                    {
                    }

                    bool oldReadOnly = false;
                    try
                    {
                        ;
                    }
                    catch (Exception e)
                    {
                        throw;
                    }
                }

                // re-calculate the evaluation queue
                if (_table != null)
                {
                    if (newExpression != null && false)
                    {
                        throw new Exception();
                    }

                    // HandleDependentColumnList(_expression, newExpression);
                    //hold onto oldExpression in case of error applying new Expression.
                    object oldExpression = _expression;
                    _expression = newExpression;

                    // because the column is attached to a table we need to re-calc values
                    try
                    {
                        if (newExpression == null)
                        {
                            Console.WriteLine("a");
                            /*
                            for (int i = 0; i < _table.RecordCapacity; i++)
                            {
                                InitializeRecord(i);
                            }
                            */
                        }
                        else
                        {
                            Console.WriteLine("b");
                            //_table.EvaluateExpressions(this);
                        }

                        Console.WriteLine("c");
                        /*
                        _table.ResetInternalIndexes(this);
                        _table.EvaluateDependentExpressions(this);
                        */
                    }
                    catch (Exception e1) when (ExceptionFilter(e1))
                    {
                        // ExceptionBuilder.TraceExceptionForCapture(e1);
                        Console.WriteLine("d");
                        try
                        {
                            // in the case of error we need to set the column expression to the old value
                            _expression = oldExpression;
                            // HandleDependentColumnList(newExpression, _expression);
                            /*
                            if (oldExpression == null)
                            {
                                for (int i = 0; i < _table.RecordCapacity; i++)
                                {
                                    InitializeRecord(i);
                                }
                            }
                            else
                            {
                                _table.EvaluateExpressions(this);
                            }
                            */
                            /*
                            _table.ResetInternalIndexes(this);
                            _table.EvaluateDependentExpressions(this);
                            */
                            Console.WriteLine("e");
                        }
                        catch (Exception e2) when (ExceptionFilter(e2))
                        {
                            Console.WriteLine("f {0}", e2);
                            // ExceptionBuilder.TraceExceptionWithoutRethrow(e2);
                        }
                        throw;
                    }
                }
                else
                {
                    //if column is not attached to a table, just set.
                    _expression = newExpression;
                }
            }
            finally
            {
                Console.WriteLine("ExitScope");
                // DataCommonEventSource.Log.ExitScope(logScopeId);
            }
        }

        static bool ExceptionFilter (Exception exc) {
            Console.WriteLine("Filter received {0}", exc.Message);
            return false;
        }
    }

    public class TestClass {
        public int J;

        public void InstanceMethodWithFilter (int i) {
            try {
                throw new Exception();
            } catch when (i == J) {
                Console.WriteLine($"i = {i} J = {J}");
            }
        }
    }

    public class TestGenericClass<T> {
        public U GenericInstanceMethodWithFilterAndIndirectReference<U> (T arg1, U arg2) {
            try {
                throw new Exception("Thrown by generic");
            } catch when (Object.Equals(arg1, default(T))) {
                var temp = new List<U>();
                temp.Add(arg2);
                return temp[0];
            }
        }
    }
}
