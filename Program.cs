using System;
using System.Diagnostics;

namespace ExceptionTransformTests {
    public struct BlittableStruct {
        int i;
    }

    public struct UnblittableStruct {
        object o;
    }

    public static class Program {
        public static void Main (string[] args) {
            Console.WriteLine("Start");
            CatchAndSilence();
            RunWithExceptionFilter();
            CatchAndSilenceNoRewrite();
            CornerCases();
            Empty();

            if (Debugger.IsAttached)
                Console.ReadLine();
        }

        static void Empty () {
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

        static int ImplementsProtocol (int i, out Exception exc) {
            BlittableStruct s = default(BlittableStruct);
            UnblittableStruct s2 = default(UnblittableStruct);

            exc = null;

            if (i < 0) {
                exc = new ArgumentOutOfRangeException("i");
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
                Console.WriteLine("Catch and silence no rewrite {0}", exc.Message);
            }
        }

        static void CornerCases () {
            try {
                ThrowsGeneric<int>(10);
            } catch {
            }

            try {
                int i;
                ThrowsWithOut(out i);
            } catch {
            }
        }

        static void CatchAndRethrow () {
            try {
                DoesNotThrow();
                Throws();
            } catch (Exception exc) {
                Console.WriteLine("Catch and rethrow {0}", exc.Message);
                throw;
            }
        }

        static void CatchAndSilence () {
            try {
                CatchAndRethrow();
            } catch (Exception exc) {
                Console.WriteLine("Catch and silence {0}", exc.Message);
            }
        }

        static void RunWithExceptionFilter () {
            try {
                Console.WriteLine("Silence with filter");
                CatchAndSilence();
                Console.WriteLine("Rethrow with filter");
                CatchAndRethrow();
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                Console.WriteLine("Caught with filter");
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
