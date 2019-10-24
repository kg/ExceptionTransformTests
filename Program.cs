using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Mono.Runtime.Internal;

namespace Mono.Runtime.Internal {
    public enum ExceptionFilterResult : int {
        NOT_EVALUATED = -1,
        exception_continue_search = 0,
        exception_execute_handler = 1
    }

    public abstract class ExceptionFilter {
        public ExceptionFilterResult Result { get; private set; }

        public static readonly ThreadLocal<List<ExceptionFilter>> ExceptionFilters = 
            new ThreadLocal<List<ExceptionFilter>>(() => new List<ExceptionFilter>(128));

        private static Exception LastEvaluatedException = null;
        private static bool HasEvaluatedFiltersAlready = false;

        public abstract ExceptionFilterResult Evaluate (Exception exc);

        public static void Push (ExceptionFilter filter) {
            filter.Result = ExceptionFilterResult.NOT_EVALUATED;
            ExceptionFilters.Value.Add(filter);
        }

        public static void Pop (ExceptionFilter filter) {
            var ef = ExceptionFilters.Value;
            if (ef.Count == 0)
                throw new ThreadStateException("Corrupt exception filter stack");
            var current = ef[ef.Count - 1];
            ef.RemoveAt(ef.Count - 1);
            if (current != filter)
                throw new ThreadStateException("Corrupt exception filter stack");
        }

        /// <summary>
        /// Resets the state of all valid exception filters so that we can handle any
        ///  new exceptions. This is invoked when a filtered block finally processes an
        ///  exception.
        /// </summary>
        public static void Reset () {
            var ef = ExceptionFilters.Value;
            foreach (var filter in ef)
                filter.Result = ExceptionFilterResult.NOT_EVALUATED;
            LastEvaluatedException = null;
        }

        /// <summary>
        /// Automatically runs any active exception filters for the exception exc, 
        ///  then returns true if the provided filter indicated that the current block
        ///  should run.
        /// </summary>
        /// <param name="exc">The exception to pass to the filters</param>
        /// <param name="filter">The exception filter for the current exception handler</param>
        /// <returns>true if this filter selected the exception handler to run</returns>
        public static bool ShouldRunHandler (Exception exc, ExceptionFilter filter) {
            if (exc == null)
                throw new ArgumentNullException("exc");
            if (filter == null)
                throw new ArgumentNullException("filter");

            PerformEvaluate(exc);
            return filter.Result == ExceptionFilterResult.exception_execute_handler;
        }

        /// <summary>
        /// Runs all active exception filters until one of them returns execute_handler.
        /// Afterward, the filters will have an initialized Result and the selected one will have
        ///  a result with the value exception_continue_search.
        /// If filters have already been run for the active exception they will not be run again.
        /// </summary>
        /// <param name="exc">The exception filters are being run for.</param>
        public static void PerformEvaluate (Exception exc) {
            if (HasEvaluatedFiltersAlready)
                return;
            // FIXME: Attempt to avoid running filters multiple times when unwinding.
            // I think this doesn't work right for rethrow?
            if (LastEvaluatedException == exc)
                return;

            var ef = ExceptionFilters.Value;
            var hasLocatedValidHandler = false;

            // Set in advance in case the filter throws.
            // These two state variables allow us to early out in the case where Evaluate() is triggered
            //  in multiple stack frames while unwinding even though filters have already run.
            LastEvaluatedException = exc;
            HasEvaluatedFiltersAlready = true;

            for (int i = ef.Count - 1; i >= 0; i--) {
                var filter = ef[i];
                if ((filter.Result = filter.Evaluate(exc)) == ExceptionFilterResult.exception_execute_handler) {
                    hasLocatedValidHandler = true;
                    break;
                }
            }

            if (!hasLocatedValidHandler)
                Console.WriteLine("Located no valid filtered handler for exception");
        }
    }
}

namespace ExceptionTransformTests {
    public class CustomExceptionFilter : ExceptionFilter {
        public override ExceptionFilterResult Evaluate (Exception exc) {
            Console.WriteLine($"CustomFilter.Evaluate({exc.Message})");
            return ExceptionFilterResult.exception_execute_handler;
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
                if (Mono.Runtime.Internal.ExceptionFilter.ShouldRunHandler(exc, customFilter))
                    Console.WriteLine("CustomFilter ran");
                else
                    Console.WriteLine($"CustomFilter result = {customFilter.Result}");
            } finally {
                Mono.Runtime.Internal.ExceptionFilter.Pop(customFilter);
            }

            CatchAndSilence();
            RunWithExceptionFilter();
            Empty();

            if (Debugger.IsAttached)
                Console.ReadLine();
        }

        [SuppressRewriting]
        static void PrintException (string message, Exception exc) {
            var ts = exc.ToString();
            var lines = ts.Replace(Environment.NewLine, "\n").Split('\n').Take(4);
            foreach (var line in lines)
                Console.WriteLine(line);
            Console.WriteLine();
        }

        static void Empty () {
        }

        static bool FilterOn (Exception exc, string s) {
            Console.WriteLine($"FilterOn({s}) processing {exc.Message}");
            return exc.Message == s;
        }

        static void NestedFilters (string s) {
            try {
                Console.WriteLine($"== NestedFilters({s}) ==");
                NestedFilters2(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters")) {
                Console.WriteLine($"NestedFilters caught {exc.Message}");
            } catch {
                Console.WriteLine("NestedFilters catch-all ran");
                throw;
            }
        }

        static void NestedFilters2 (string s) {
            try {
                NestedFilters3(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters2")) {
                Console.WriteLine($"NestedFilters2 caught {exc.Message}");
            }
        }

        static void NestedFilters3 (string s) {
            try {
                throw new Exception(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters3")) {
                Console.WriteLine($"NestedFilters3 caught {exc.Message}");
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
