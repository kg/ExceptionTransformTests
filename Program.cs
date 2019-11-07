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

        static bool ExceptionFilter (Exception exc) {
            Console.WriteLine("Filter received {0}", exc.Message);
            return false;
        }
    }

    public class SuppressRewritingAttribute : Attribute {
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
        public U GenericInstanceMethodWithFilter<U> (T arg1, U arg2) {
            try {
                throw new Exception();
                return arg2;
            } catch when (Object.Equals(arg1, default(T))) {
                return default(U);
            }
        }

        public T InstanceMethodWithFilter (T arg) {
            try {
                throw new Exception();
                return arg;
            } catch when (Object.Equals(arg, default(T))) {
                return default(T);
            }
        }
    }
}
