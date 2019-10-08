using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {

    struct DependencyCollection {
        private readonly List<(INotifyPropertyChanged Source, string Property, int Cookie)> _storage;
        private readonly int _cookie;

        public DependencyCollection(List<(INotifyPropertyChanged Source, string Property, int Cookie)> storage, int cookie) {
            _storage = storage;
            _cookie = cookie;
        }

        public TSource AddDependency<TSource>(TSource source, string property) where TSource : INotifyPropertyChanged {
            _storage.Add((source, property, _cookie));
            return source;
        }
    }

    delegate TResult ComputeAndCollectDependencies<TObj, TResult>(TObj source, ref DependencyCollection collector);

    static class DependencyCollector {
        private static ParameterExpression? _dependenciesParameter;
        private static MethodInfo? _addMethod;
        private static Visitor? _visitor;

        public static ComputeAndCollectDependencies<TObj, TResult> Create<TObj, TResult>(Expression<Func<TObj, TResult>> expression) {
            if (_dependenciesParameter == null) _dependenciesParameter = Expression.Parameter(typeof(DependencyCollection).MakeByRefType(), "dependencies");
            if (_addMethod == null) _addMethod = typeof(DependencyCollection).GetMethod("AddDependency");
            if (_visitor == null) _visitor = new Visitor();

            var newBody = _visitor.Visit(expression.Body);
            var newLambda = Expression.Lambda<ComputeAndCollectDependencies<TObj, TResult>>(newBody, expression.Parameters[0], _dependenciesParameter);
            return newLambda.Compile();
        }

        private class Visitor : ExpressionVisitor {
            protected override Expression VisitMember(MemberExpression node) {
                var expression = Visit(node.Expression);
                if (node.Member is PropertyInfo && typeof(INotifyPropertyChanged).IsAssignableFrom(expression.Type) && !expression.Type.IsValueType) {
                    var addMethod = _addMethod!.MakeGenericMethod(expression.Type);
                    return Expression.MakeMemberAccess(Expression.Call(_dependenciesParameter, addMethod, expression, Expression.Constant(node.Member.Name)), node.Member);
                }
                else {
                    return node.Expression == expression ? node : Expression.MakeMemberAccess(expression, node.Member);
                }
            }
        }
    }

}
