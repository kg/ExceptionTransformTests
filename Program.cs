using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ExceptionTransformTests {
    public static class Program {
        public static void Main (string[] args) {
            Console.WriteLine("Start");
            NestedFilters("NestedFilters");
            NestedFilters("NestedFilters3");

            CatchAndSilence();
            RunWithExceptionFilter();
            MultipleTypedCatches();
            ThrowInsideBrokenFilter();

            var c = new C();
            c.NestedFiltersInOneFunction("value");
            c.LopsidedWithFinally();
            c.TestReturnValueWithFinallyAndDefault();
            int x = 5;
            float y = 7;
            c.TestRefParam(ref x, ref y);
            Console.WriteLine($"x = {x}, y = {y}");

            var tempv = new TestGenericClass<int>();
            var ret = tempv.GenericInstanceMethodWithFilterAndIndirectReference(default(int), "test");
            Console.WriteLine("ret=" + ret);

            Console.WriteLine("test_0_filter_caller_area={0}", MiniTests.test_0_filter_caller_area());
            Console.WriteLine("test_1234_complicated_filter_catch={0}", MiniTests.test_1234_complicated_filter_catch());
            Console.WriteLine("test_1_basic_filter_catch={0}", MiniTests.test_1_basic_filter_catch());

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
                throw new FieldAccessException("Thrown by MultipleTypedCatches on purpose");
            } catch (InvalidOperationException ioe) when (true) {
                Console.WriteLine("MultipleTypedCatches IOE");
            } catch (NullReferenceException nre) {
                Console.WriteLine("MultipleTypedCatches NRE");
            } catch {
                Console.WriteLine("MultipleTypedCatches catch");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool FilterOn (Exception exc, string s) {
            Console.WriteLine($"FilterOn({s}) processing '{exc.Message}'");
            return exc.Message == s;
        }

        static void NestedFilters (string s) {
            try {
                Console.WriteLine($"== NestedFilters({s}) ==");
                NestedFilters2(s);
                Console.WriteLine("NestedFilters2 didn't throw?");
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters") || false) {
                Console.WriteLine($"NestedFilters caught '{exc.Message}' via filter");
            } catch {
                Console.WriteLine($"NestedFilters caught unknown via fallback");
                throw;
            }
            Console.WriteLine("NestedFilters left try and catch");
        }

        static void NestedFilters2 (string s) {
            try {
                NestedFilters3(s);
                Console.WriteLine("NestedFilters3 didn't throw?");
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters2") || (exc.Message == "nope")) {
                Console.WriteLine($"NestedFilters2 caught '{exc.Message}' via filter");
            }
        }

        static void NestedFilters3 (string s) {
            try {
                throw new Exception(s);
            } catch (Exception exc) when (FilterOn(exc, "NestedFilters3")) {
                Console.WriteLine($"NestedFilters3 caught '{exc.Message}' via filter");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Throws () {
            throw new Exception("Throws");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoesNotThrow () {
            Console.WriteLine("DoesNotThrow");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
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

        static void ThrowInsideBrokenFilter () {
            try {
                try {
                    throw new Exception("test");
                } catch (Exception exc) when (BrokenExceptionFilter(exc)) {
                    Console.WriteLine("Exc handler was selected by broken filter");
                } catch {
                    Console.WriteLine("Fall-through catch in broken function rethrowing");
                    throw;
                }
            } catch (Exception exc) {
                Console.WriteLine($"Outer catch enclosing broken filter caught exception {exc.GetType().Name}: {exc.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool BrokenExceptionFilter (Exception exc) {
            throw new Exception("Broken filter", exc);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ExceptionFilter (Exception exc) {
            Console.WriteLine("Filter received '{0}'", exc.Message);
            return false;
        }
    }

    public class C {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ExceptionFilter (Exception exc) {
            Console.WriteLine("Filter received '{0}'", exc.Message);
            return (exc.Message == "catch");
        }

        bool a = true;
        bool b = false;

        public void NestedFiltersInOneFunction (string value) {
            object _expression = null;
            long logScopeId = 0; // DataCommonEventSource.Log.EnterScope("<ds.DataColumn.set_Expression|API> {0}, '{1}'", ObjectID, value);
            object _table = null;

            if (value == null) {
                value = string.Empty;
            }

            try {
                object newExpression = null;
                if (value.Length > 0) {
                    object testExpression = new object();
                    if (a)
                        newExpression = testExpression;
                }

                if (_expression == null && newExpression != null) {
                    if (b) {
                        throw new Exception();
                    }

                    // We need to make sure the column is not involved in any Constriants
                    if (_table != null) {
                    }

                    bool oldReadOnly = false;
                    try {
                        Console.WriteLine("Dead try");
                    } catch (Exception e) {
                        Console.WriteLine("Dead catch");
                        throw;
                    }
                }

                // re-calculate the evaluation queue
                if (_table != null) {
                    if (newExpression != null && ReturnsFalse()) {
                        throw new Exception();
                    }

                    // HandleDependentColumnList(_expression, newExpression);
                    //hold onto oldExpression in case of error applying new Expression.
                    object oldExpression = _expression;
                    _expression = newExpression;

                    // because the column is attached to a table we need to re-calc values
                    try {
                        if (newExpression == null) {
                            Console.WriteLine("a");
                            for (int i = 0; i < 10; i++) {
                                ReturnsFalse();
                                // InitializeRecord(i);
                            }
                        } else {
                            Console.WriteLine("b");
                            //_table.EvaluateExpressions(this);
                        }

                        Console.WriteLine("c");
                        /*
                        _table.ResetInternalIndexes(this);
                        _table.EvaluateDependentExpressions(this);
                        */
                    } catch (Exception e1) when (ExceptionFilter(e1)) {
                        // ExceptionBuilder.TraceExceptionForCapture(e1);
                        Console.WriteLine("d");
                        try {
                            // in the case of error we need to set the column expression to the old value
                            _expression = oldExpression;
                            ReturnsFalse();
                            // HandleDependentColumnList(newExpression, _expression);
                            if (oldExpression == null) {
                                for (int i = 0; i < 10; i++) {
                                    ReturnsFalse();
                                    // InitializeRecord(i);
                                }
                            } else {
                                ReturnsFalse(this);
                                // _table.EvaluateExpressions(this);
                            }
                            ReturnsFalse(this);
                            /*
                            _table.ResetInternalIndexes(this);
                            _table.EvaluateDependentExpressions(this);
                            */
                        } catch (Exception e2) when (ExceptionFilter(e2)) {
                            Console.WriteLine("f {0}", e2);
                            // ExceptionBuilder.TraceExceptionWithoutRethrow(e2);
                        }
                        throw;
                    }
                } else {
                    //if column is not attached to a table, just set.
                    _expression = newExpression;
                }
            } finally {
                Console.WriteLine("ExitScope");
                // DataCommonEventSource.Log.ExitScope(logScopeId);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int One (bool b) {
            if (b) return 1;
            else return 0;
        }

        public void Lopsided () {
            int i = 0;
            try {
                i += One(true);
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                Console.WriteLine("Layer 1");
                i += One(true);
                try {
                    i += One(true);
                } catch (Exception exc2) when (ExceptionFilter(exc2)) {
                    Console.WriteLine("Layer 2");
                    i += One(true);
                    try {
                        i += One(true);
                    } catch (Exception exc3) when (ExceptionFilter(exc3)) {
                        Console.WriteLine("Layer 3");
                        i += One(true);
                        try {
                            i += One(true);
                        } catch (Exception exc4) when (ExceptionFilter(exc4)) {
                            Console.WriteLine("Layer 4");
                            i += One(true);
                            if (One(false) == 1)
                                throw;
                        }

                        if (One(true) == 1)
                            throw;
                    }
                }
            }
        }

        public void LopsidedWithFinally () {
            int i = 0;
            try {
                i += One(true);
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                Console.WriteLine("Layer 1");
                i += One(true);
                try {
                    i += One(true);
                } catch (Exception exc2) when (ExceptionFilter(exc2)) {
                    Console.WriteLine("Layer 2");
                    i += One(true);
                    try {
                        i += One(true);
                    } catch (Exception exc3) when (ExceptionFilter(exc3)) {
                        Console.WriteLine("Layer 3");
                        i += One(true);
                        try {
                            i += One(true);
                        } catch (Exception exc4) when (ExceptionFilter(exc4)) {
                            Console.WriteLine("Layer 4");
                            i += One(true);
                            if (One(false) == 1)
                                throw;
                        } finally {
                            Console.WriteLine("Innermost finally");
                        }

                        if (One(true) == 1)
                            throw;
                    }
                }
            } finally {
                Console.WriteLine("Outmost finally");
            }
        }

        public void TestReturns () {
            int i = 0;
            try {
                i += One(true);
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                Console.WriteLine("Layer 1");
                i += One(true);
                return;
            }
        }

        public float TestReturnValue () {
            int i = 0;
            try {
                i += One(true);
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                Console.WriteLine("Layer 1");
                i += One(true);
                return 3.0f;
            }

            return 1.0f;
        }

        public float TestReturnValueWithFinallyAndDefault () {
            int i = 0;
            try {
                i += One(true);
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                Console.WriteLine("Layer 1");
                i += One(true);
                return 3.0f;
            } catch {
                Console.WriteLine("Fallback catch");
                i += One(true);
                return 4.0f;
            } finally {
                Console.WriteLine("Outmost finally");
            }

            return 2.0f;
        }

        public void TestRefParam (ref int a, ref float b) {
            a += 1;
            b += 1.5f;

            try {
                b -= a;
                throw new Exception("catch");
                b += 3;
            } catch (Exception exc) when (ExceptionFilter(exc)) {
                a += 2;
                b += 3.5f;
            } finally {
                a -= 1;
                b += 3f;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ReturnsFalse (C self) {
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool ReturnsFalse () {
            return false;
        }
    }

    public class WebRequest { }

    public class WebException : Exception {
        public WebException (string message, Exception innerException)
            : base(message, innerException) {
        }
    }

    public class SecurityException : Exception { }

    public class ChunkedMemoryStream : MemoryStream {
        public ChunkedMemoryStream () {
        }
    }

    public class WebClient {
        WebRequest _webRequest;

        public static Uri GetUri (string address) {
            return new Uri(address);
        }

        public static Uri GetUri (Uri uri) {
            return uri;
        }

        public WebRequest GetWebRequest (Uri uri) {
            return new WebRequest();
        }

        public static void ThrowIfNull<T> (T value, string name) {
            if (value == null)
                throw new ArgumentNullException(name);
        }

        public void StartOperation () {
        }

        public void EndOperation () {
        }

        public void AbortRequest (WebRequest request) {
        }

        private byte[] DownloadBits (WebRequest request, Stream s) {
            return null;
        }

        // FIXME: This method only reproduces the related issue if it is optimized
        public void DownloadFile (Uri address, string fileName) {
            ThrowIfNull(address, nameof(address));
            ThrowIfNull(fileName, nameof(fileName));

            WebRequest request = null;
            FileStream fs = null;
            bool succeeded = false;
            StartOperation();
            try {
                fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                request = _webRequest = GetWebRequest(GetUri(address));
                DownloadBits(request, fs);
                succeeded = true;
            } catch (Exception e) when (!(e is OutOfMemoryException)) {
                AbortRequest(request);
                if (e is WebException || e is SecurityException) throw;
                throw new WebException("SR.net_webclient", e);
            } finally {
                if (fs != null) {
                    fs.Close();
                    if (!succeeded) {
                        File.Delete(fileName);
                    }
                }
                EndOperation();
            }
        }

        private byte[] DownloadDataInternal (Uri address, out WebRequest request) {
            WebRequest tmpRequest = null;
            byte[] result;

            try {
                tmpRequest = _webRequest = GetWebRequest(GetUri(address));
                result = DownloadBits(tmpRequest, new ChunkedMemoryStream());
            } catch (Exception e) when (!(e is OutOfMemoryException)) {
                AbortRequest(tmpRequest);
                if (e is WebException || e is SecurityException) throw;
                throw new WebException("SR.net_webclient", e);
            }

            request = tmpRequest;
            return result;
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

    public static class MiniTests {
        public class MyException : Exception {
            public int marker = 0;
            public string res = "";

            public MyException (String res) {
                this.res = res;
            }

            public bool FilterWithoutState () {
                var ret = this.marker == 0x666;
                Console.WriteLine($"FilterWithoutState returning {ret}");
                return ret;
            }

            public bool FilterWithState () {
                bool ret = this.marker == 0x566;
                this.marker += 0x100;
                Console.WriteLine($"FilterWithState returning {ret}");
                return ret;
            }

            public bool FilterWithStringState () {
                bool ret = this.marker == 0x777;
                this.res = "fromFilter_" + this.res;
                Console.WriteLine($"FilterWithStringState returning {ret}");
                return ret;
            }
        }

        public static int test_1_basic_filter_catch () {
            try {
                MyException e = new MyException("");
                e.marker = 0x1337;
                throw e;
            } catch (MyException ex) when (ex.marker == 0x1337) {
                return 1;
            }
            return 0;
        }

        public static int test_1234_complicated_filter_catch () {
            string res = "init";
            try {
                MyException e = new MyException(res);
                e.marker = 0x566;
                try {
                    try {
                        throw e;
                    } catch (MyException ex) when (ex.FilterWithoutState()) {
                        res = "WRONG_" + res;
                    } finally {
                        e.marker = 0x777;
                        res = "innerFinally_" + res;
                    }
                } catch (MyException ex) when (ex.FilterWithState()) {
                    res = "2ndcatch_" + res;
                }
                // "2ndcatch_innerFinally_init"
                // Console.WriteLine ("res1: " + res);
                e.res = res;
                throw e;
            } catch (MyException ex) when (ex.FilterWithStringState()) {
                res = "fwos_" + ex.res;
            } finally {
                res = "outerFinally_" + res;
            }
            // Console.WriteLine ("res2: " + res);
            return "outerFinally_fwos_fromFilter_2ndcatch_innerFinally_init" == res ? 1234 : 0;
        }

        public struct FooStruct {
            public long Part1 { get; }
            public long Part2 { get; }

            public byte Part3 { get; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ExceptionFilter (byte x, FooStruct item) => true;

        public static int test_0_filter_caller_area () {
            try {
                throw new Exception();
            } catch (Exception) when (ExceptionFilter(default(byte), default(FooStruct))) {
            }
            return 0;
        }
    }
}
