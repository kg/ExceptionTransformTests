using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ExceptionTransformTests {
    public enum __IExceptionFilterResult : int {
        NOT_EVALUATED = -1,
        exception_continue_search = 0,
        exception_execute_handler = 1
    }

    public interface __IExceptionFilter {
        __IExceptionFilterResult Result { set; }
        __IExceptionFilterResult Evaluate (Exception exc);
    }

    public class CustomExceptionFilter : __IExceptionFilter {
        public __IExceptionFilterResult Result { get; set; } = __IExceptionFilterResult.NOT_EVALUATED;

        public __IExceptionFilterResult Evaluate (Exception exc) {
            Console.WriteLine($"CustomFilter.Evaluate({exc.Message}");
            return __IExceptionFilterResult.exception_execute_handler;
        }
    }

    public static class __ExceptionFilterImpl {
        public static readonly ThreadLocal<List<__IExceptionFilter>> ExceptionFilters = 
            new ThreadLocal<List<__IExceptionFilter>>(() => new List<__IExceptionFilter>(128));

        public static void Push (__IExceptionFilter filter) {
            filter.Result = __IExceptionFilterResult.NOT_EVALUATED;
            ExceptionFilters.Value.Add(filter);
        }

        public static void Pop (__IExceptionFilter filter) {
            var ef = ExceptionFilters.Value;
            if (ef.Count == 0)
                throw new ThreadStateException("Corrupt exception filter stack");
            var current = ef[ef.Count - 1];
            ef.RemoveAt(ef.Count - 1);
            if (current != filter)
                throw new ThreadStateException("Corrupt exception filter stack");
        }

        public static void Evaluate (Exception exc) {
            var ef = ExceptionFilters.Value;
            for (int i = ef.Count - 1; i >= 0; i--) {
                var filter = ef[i];
                filter.Result = filter.Evaluate(exc);
            }
        }
    }

    public struct BlittableStruct {
        int i;
    }

    public struct UnblittableStruct {
        object o;
    }

    public static class Program {
        public static void Main (string[] args) {
            Console.WriteLine("Start");
            NestedFilters("NestedFilters");
            NestedFilters("NestedFilters3");

            var customFilter = new CustomExceptionFilter();
            __ExceptionFilterImpl.Push(customFilter);
            try {
                NestedFilters("RunCustomFilter");
            } catch {
                Console.WriteLine($"CustomFilter result = {customFilter.Result}");
            } finally {
                __ExceptionFilterImpl.Pop(customFilter);
            }

            CatchAndSilence();
            RunWithExceptionFilter();
            CatchAndSilenceNoRewrite();
            CornerCases();
            Empty();

            if (Debugger.IsAttached)
                Console.ReadLine();
        }

        [SuppressRewriting]
        static void PrintException (string message, Exception exc) {
            Console.WriteLine(exc);
        }

        static void Empty () {
        }

        static bool FilterOn (Exception exc, string s) {
            Console.WriteLine($"FilterOn({s}) processing {exc.Message}");
            return exc.Message == s;
        }

        static void NestedFilters (string s) {
            try {
                NestedFilters2(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters")) {
            }
        }

        static void NestedFilters2 (string s) {
            try {
                NestedFilters3(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters2")) {
            }
        }

        static void NestedFilters3 (string s) {
            try {
                throw new Exception(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters3")) {
            }
        }

        static void Throws () {
            throw new Exception("Throws");
        }

        static void DoesNotThrow () {
            Console.WriteLine("DoesNotThrow");
        }

        static void ThrowsGeneric<T> (T value) {
            throw new Exception("ThrowsGeneric");
        }

        static int ThrowsWithResult (int i) {
            throw new Exception("ThrowsWithResult");
            return i;
        }

        static void ThrowsWithOut(out int o) {
            o = 5;
            throw new Exception("ThrowsWithOut");
        }

        static void MightThrow (bool b) {
            if (b)
                throw new Exception("MightThrow");
        }

        static BlittableStruct ThrowsOrReturnsStruct (bool b) {
            if (b)
                throw new Exception("ThrowsOrReturnsStruct");
            else
                return default(BlittableStruct);
        }

        static int ImplementsProtocol (int i, out ExceptionDispatchInfo exc) {
            BlittableStruct s = default(BlittableStruct);
            UnblittableStruct s2 = default(UnblittableStruct);

            exc = null;

            if (i < 0) {
                exc = ExceptionDispatchInfo.Capture(new ArgumentOutOfRangeException("i"));
                return default(int);
            } else {
                return i;
            }
        }

        [SuppressRewriting]
        static void CatchAndSilenceNoRewrite () {
            try {
                throw new Exception("NoRewrite");
            } catch (Exception exc) {
                PrintException("Catch and silence no rewrite", exc);
            }
        }

        static void CornerCases () {
            try {
                ThrowsGeneric<int>(10);
            } catch (Exception exc) {
                PrintException("ThrowsGeneric", exc);
            }

            try {
                int i;
                ThrowsWithOut(out i);
            } catch (Exception exc) {
                PrintException("ThrowsWithOut", exc);
            }
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
}
