using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Iftm.ComputedProperties.Test {

    class ExpectChanges : IDisposable {
        private readonly List<string> _expected;
        private readonly INotifyPropertyChanged _source;

        public ExpectChanges(INotifyPropertyChanged source, params string[] expected) {
            _expected = expected.Distinct().ToList();
            Assert.Equal(expected.Length, _expected.Count);
            
            _source = source;
            _source.PropertyChanged += OnSourcePropertyChanged;
        }

        private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e) {
            Assert.NotNull(e.PropertyName);
            Assert.Contains(e.PropertyName, _expected);
            _expected.Remove(e.PropertyName);
        }

        public void Dispose() {
            _source.PropertyChanged -= OnSourcePropertyChanged;
            Assert.Empty(_expected);
        }
    }

    public class ComputedPropertiesTests {
        private ExpectChanges ExpectChanges(INotifyPropertyChanged source, params string[] expected) =>
            new ExpectChanges(source, expected);

        class Test1 : WithComputedProperties {
            private static ComputedProperty<Test1, T> Computed<T>(Expression<Func<Test1, T>> expression) => new ComputedProperty<Test1, T>(expression);

            private int _a;
            public int A {
                get => _a;
                set => SetProperty(ref _a, value);
            }

            private int _b;
            public int B {
                get => _b;
                set => SetProperty(ref _b, value);
            }

            private static readonly ComputedProperty<Test1, int> _c = Computed(test => test.A + 1);
            public int C => _c.Eval(this);

            private static readonly ComputedProperty<Test1, int> _d = Computed(test => test.B + test.C);
            public int D => _d.Eval(this);
        }

        [Fact]
        public void TestSimple() {
            var obj = new Test1();

            Assert.False(obj.HasListeners);
            Assert.Equal(0, obj.A);
            Assert.Equal(0, obj.B);
            Assert.Equal(1, obj.C);
            Assert.Equal(1, obj.D);

            using (ExpectChanges(obj, "A", "C", "D")) {
                Assert.True(obj.HasListeners);
                obj.A = 5;
            }

            Assert.False(obj.HasListeners);
            Assert.Equal(5, obj.A);
            Assert.Equal(0, obj.B);
            Assert.Equal(6, obj.C);
            Assert.Equal(6, obj.D);

            using (ExpectChanges(obj, "B", "D")) {
                Assert.True(obj.HasListeners);
                obj.B = 10;
            }

            Assert.False(obj.HasListeners);
            Assert.Equal(5, obj.A);
            Assert.Equal(10, obj.B);
            Assert.Equal(6, obj.C);
            Assert.Equal(16, obj.D);
        }

        class Test2 : Test1 {
            private bool _e;
            public bool E {
                get => _e;
                set => SetProperty(ref _e, value);
            }

            private static readonly ComputedProperty<Test2, int> _f = Computed((Test2 obj) => obj.E ? obj.A : obj.B + obj.D);
            public int F => _f.Eval(this);
        }

        [Fact]
        public void TestConditional() {
            var obj = new Test2();
            Assert.False(obj.HasListeners);

            var f = obj.F;

            Assert.Equal(0, obj.A);
            Assert.Equal(0, obj.B);
            Assert.Equal(1, obj.C);
            Assert.Equal(1, obj.D);
            Assert.Equal(1, obj.F);

            using (ExpectChanges(obj, "A", "C", "D", "F")) {
                obj.A = 5;
                
                Assert.True(obj.HasListeners);
                Assert.Equal(5, obj.A);
                Assert.Equal(0, obj.B);
                Assert.Equal(6, obj.C);
                Assert.Equal(6, obj.D);
                Assert.Equal(6, obj.F);
            }

            using (ExpectChanges(obj, "B", "D", "F")) {
                obj.B = 10;

                Assert.True(obj.HasListeners);
                Assert.Equal(5, obj.A);
                Assert.Equal(10, obj.B);
                Assert.Equal(6, obj.C);
                Assert.Equal(16, obj.D);
                Assert.Equal(26, obj.F);
            }

            using (ExpectChanges(obj, "E", "F")) {
                obj.E = true;

                Assert.True(obj.HasListeners);
                Assert.Equal(5, obj.A);
                Assert.Equal(5, obj.F);
            }
        }

        class Test3 : WithComputedProperties {
            private Test1 _a = new Test1();
            private Test1 _b = new Test1();
            private Test1 _c = new Test1();
            private Test1 _d = new Test1();
            private bool _e, _f, _g, _h;

            public Test1 A {
                get => _a;
                set => SetProperty(ref _a, value);
            }

            public Test1 B {
                get => _b;
                set => SetProperty(ref _b, value);
            }

            public Test1 C {
                get => _c;
                set => SetProperty(ref _c, value);
            }

            public Test1 D {
                get => _d;
                set => SetProperty(ref _d, value);
            }

            public bool E {
                get => _e;
                set => SetProperty(ref _e, value);
            }

            public bool F {
                get => _f;
                set => SetProperty(ref _f, value);
            }

            public bool G {
                get => _g;
                set => SetProperty(ref _g, value);
            }

            public bool H {
                get => _h;
                set => SetProperty(ref _h, value);
            }

            private static readonly ComputedProperty<Test3, int> _i = Computed((Test3 obj) => obj.E ? obj.A.A + obj.A.B : 0);
            public int I => _i.Eval(this);

            private static readonly ComputedProperty<Test3, int> _j = Computed((Test3 obj) => obj.F ? obj.A.A : obj.B.A + obj.B.B);
            public int J => _j.Eval(this);

            private static readonly ComputedProperty<Test3, int> _k = Computed((Test3 obj) => obj.G ? obj.I : 0);
            public int K => _k.Eval(this);

            private static readonly ComputedProperty<Test3, int> _m = Computed((Test3 obj) => obj.H ? obj.J : 0);
            public int M => _m.Eval(this);
        }

        private static void Eval<T>(params T[] x) {
        }

        [Fact]
        public void TestSubscriptions() {
            void OnNotifyPropertyChanged(object sender, PropertyChangedEventArgs args) {
            }

            var obj = new Test3();
            obj.PropertyChanged += OnNotifyPropertyChanged;
            Eval(obj.I, obj.J, obj.K, obj.M);

            Assert.False(obj.A.HasListeners);
            Assert.True(obj.B.HasListeners);
            Assert.False(obj.C.HasListeners);
            Assert.False(obj.D.HasListeners);

            using (ExpectChanges(obj, "E", "I")) {
                obj.E = true;
                Eval(obj.I, obj.J, obj.K, obj.M);
                Assert.True(obj.A.HasListeners);
                Assert.True(obj.B.HasListeners);
                Assert.False(obj.C.HasListeners);
                Assert.False(obj.D.HasListeners);
            }

            using (ExpectChanges(obj, "F", "J")) {
                obj.F = true;
                Eval(obj.I, obj.J, obj.K, obj.M);
                Assert.True(obj.A.HasListeners);
                Assert.False(obj.B.HasListeners);
                Assert.False(obj.C.HasListeners);
                Assert.False(obj.D.HasListeners);
            }

            using (ExpectChanges(obj, "E", "I")) {
                obj.E = false;
                Eval(obj.I, obj.J, obj.K, obj.M);
                Assert.True(obj.A.HasListeners);
                Assert.False(obj.B.HasListeners);
                Assert.False(obj.C.HasListeners);
                Assert.False(obj.D.HasListeners);
            }

            using (ExpectChanges(obj, "F", "J")) {
                obj.F = false;
                Eval(obj.I, obj.J, obj.K, obj.M);
                Assert.False(obj.A.HasListeners);
                Assert.True(obj.B.HasListeners);
                Assert.False(obj.C.HasListeners);
                Assert.False(obj.D.HasListeners);
            }

            using (ExpectChanges(obj, "H", "M")) {
                obj.H = true;
                Eval(obj.I, obj.J, obj.K, obj.M);
            }

            using (ExpectChanges(obj, "F", "J", "M")) {
                obj.F = true;
                Eval(obj.I, obj.J, obj.K, obj.M);
                Assert.True(obj.A.HasListeners);
                Assert.False(obj.B.HasListeners);
                Assert.False(obj.C.HasListeners);
                Assert.False(obj.D.HasListeners);
            }

            var prevA = obj.A;
            Assert.True(prevA.HasListeners);
            using (ExpectChanges(obj, "A", "J", "M")) {
                obj.A = new Test1();
                Eval(obj.I, obj.J, obj.K, obj.M);

                Assert.False(prevA.HasListeners);
                Assert.True(obj.A.HasListeners);
                Assert.False(obj.B.HasListeners);
                Assert.False(obj.C.HasListeners);
                Assert.False(obj.D.HasListeners);
            }

            obj.PropertyChanged -= OnNotifyPropertyChanged;

            Assert.False(obj.HasListeners);
            Assert.False(obj.A.HasListeners);
            Assert.False(obj.B.HasListeners);
            Assert.False(obj.C.HasListeners);
            Assert.False(obj.D.HasListeners);
        }
    }

}
