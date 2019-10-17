using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {

    public struct ComputedProperty<TObj, TResult> {
        private readonly ComputeAndCollectDependencies<TObj, TResult> _func;

        public ComputedProperty(Expression<Func<TObj, TResult>> expression) {
            _func = DependencyCollector.Create (expression);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult CallAndGetDependencies(TObj obj, List<(INotifyPropertyChanged Source, string Property, int Cookie)> dependencies, int cookie) {
            var collection = new DependencyCollection(dependencies, cookie);
            return _func(obj, ref collection);
        }
    }

    public interface IDependenciesTarget {
        void SetDependencies(string property, List<(INotifyPropertyChanged Source, string Property, int Cookie)> dependencies,int cookie);
    }

    public interface IIsPropertyValid {
        bool IsPropertyValid(string name);
        void SetPropertyValid(string name);
    }


    static class ComputedPropertyStorage {
        private class CookieList : List<(INotifyPropertyChanged Source, string Property, int Cookie)> {
            public int Cookie;
        }

        [ThreadStatic] private static CookieList? _list;

        public static (List<(INotifyPropertyChanged Source, string Property, int Cookie)> List, int Cookie) GetDependencyStorage() {
            if (_list == null) _list = new CookieList();

            return (_list, ++_list.Cookie);
        }
    }

    public static class ComputedProperty {
        public static TResult Eval<TObj, TResult>(this ComputedProperty<TObj, TResult> property, TObj obj, [CallerMemberName] string? name = null) where TObj : IDependenciesTarget {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var (dependencies, cookie) = ComputedPropertyStorage.GetDependencyStorage();
            var listEmpty = dependencies.Count == 0;
            try {
                return property.CallAndGetDependencies(obj, dependencies, cookie);
            }
            finally {
                obj.SetDependencies(name, dependencies, cookie);
                RemoveMatchingInputs(dependencies, cookie);
                Debug.Assert(!listEmpty || dependencies.Count == 0);
            }
        }

        public static TResult Eval<TObj, TResult>(this ComputedProperty<TObj, TResult> property, TObj obj, ref TResult lastVal, [CallerMemberName] string? name = null) where TObj : IDependenciesTarget, IIsPropertyValid {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (obj.IsPropertyValid(name)) {
                return lastVal;
            }
            else {
                var result = Eval(property, obj, name);
                lastVal = result;
                obj.SetPropertyValid(name);
                return result;
            }
        }

        private static void RemoveMatchingInputs(List<(INotifyPropertyChanged Source, string Property, int Cookie)> list, int cookie) {
            int writePos = 0;
            int count = list.Count;

            for (int x = 0; x < count; ++x) {
                var y = list[x];
                if (y.Cookie != cookie) {
                    list[writePos++] = y;
                }
            }

            list.RemoveRange(writePos, list.Count - writePos);
        }
    }


}
