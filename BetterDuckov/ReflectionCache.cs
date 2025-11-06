using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace bigInventory
{
    public static class ReflectionCache
    {
        private static readonly ConcurrentDictionary<string, FieldInfo> FieldCache =
            new ConcurrentDictionary<string, FieldInfo>();
        private static readonly ConcurrentDictionary<string, MethodInfo> MethodCache =
            new ConcurrentDictionary<string, MethodInfo>();
        private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache =
            new ConcurrentDictionary<string, PropertyInfo>();

        private static readonly ConcurrentDictionary<string, Func<object, object>> FieldGetterCache =
            new ConcurrentDictionary<string, Func<object, object>>();
        private static readonly ConcurrentDictionary<string, Action<object, object>> FieldSetterCache =
            new ConcurrentDictionary<string, Action<object, object>>();
        private static readonly ConcurrentDictionary<string, Func<object, object>> PropertyGetterCache =
            new ConcurrentDictionary<string, Func<object, object>>();
        private static readonly ConcurrentDictionary<string, Action<object, object>> PropertySetterCache =
            new ConcurrentDictionary<string, Action<object, object>>();
        private static readonly ConcurrentDictionary<string, Func<object, object[], object>> MethodInvokerCache =
            new ConcurrentDictionary<string, Func<object, object[], object>>();

        private static string KeyFor(Type t, string name, string tag)
        {
            return t.FullName + "::" + name + "::" + tag;
        }

        public static FieldInfo GetFieldCached(Type type, string name,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string key = KeyFor(type, name, "FIELD");
            FieldInfo fi;
            if (FieldCache.TryGetValue(key, out fi)) return fi;
            fi = type.GetField(name, flags);
            if (fi != null) FieldCache[key] = fi;
            return fi;
        }

        public static PropertyInfo GetPropertyCached(Type type, string name,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string key = KeyFor(type, name, "PROP");
            PropertyInfo pi;
            if (PropertyCache.TryGetValue(key, out pi)) return pi;
            pi = type.GetProperty(name, flags);
            if (pi != null) PropertyCache[key] = pi;
            return pi;
        }

        public static MethodInfo GetMethodCached(Type type, string name, Type[] parameterTypes = null,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string args = "";
            if (parameterTypes != null)
            {
                List<string> names = new List<string>();
                foreach (var t in parameterTypes) names.Add(t.FullName);
                args = string.Join(",", names.ToArray());
            }

            string key = KeyFor(type, name, "METHOD_" + args);
            MethodInfo mi;
            if (MethodCache.TryGetValue(key, out mi)) return mi;

            mi = parameterTypes == null
                ? type.GetMethod(name, flags)
                : type.GetMethod(name, flags, null, parameterTypes, null);
            if (mi != null) MethodCache[key] = mi;
            return mi;
        }

        // ------------------ Field Getter/Setter ------------------
        public static Func<object, object> CreateFieldGetter(Type type, string fieldName,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string key = KeyFor(type, fieldName, "GETFIELD");
            Func<object, object> getter;
            if (FieldGetterCache.TryGetValue(key, out getter)) return getter;

            FieldInfo fi = GetFieldCached(type, fieldName, flags);
            if (fi == null)
            {
                getter = delegate (object _) { return null; };
                FieldGetterCache[key] = getter;
                return getter;
            }

            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            UnaryExpression castInstance = Expression.Convert(instanceParam, type);
            MemberExpression fieldAccess = Expression.Field(castInstance, fi);
            UnaryExpression convertResult = Expression.Convert(fieldAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(convertResult, instanceParam);
            getter = lambda.Compile();
            FieldGetterCache[key] = getter;
            return getter;
        }

        public static Action<object, object> CreateFieldSetter(Type type, string fieldName,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string key = KeyFor(type, fieldName, "SETFIELD");
            Action<object, object> setter;
            if (FieldSetterCache.TryGetValue(key, out setter)) return setter;

            FieldInfo fi = GetFieldCached(type, fieldName, flags);
            if (fi == null)
            {
                setter = delegate (object _, object __) { };
                FieldSetterCache[key] = setter;
                return setter;
            }

            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");
            UnaryExpression castInstance = Expression.Convert(instanceParam, type);
            UnaryExpression castValue = Expression.Convert(valueParam, fi.FieldType);
            MemberExpression fieldAccess = Expression.Field(castInstance, fi);
            BinaryExpression assign = Expression.Assign(fieldAccess, castValue);
            var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);
            setter = lambda.Compile();
            FieldSetterCache[key] = setter;
            return setter;
        }

        // ------------------ Property Getter/Setter ------------------
        public static Func<object, object> CreatePropertyGetter(Type type, string propertyName,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string key = KeyFor(type, propertyName, "GETPROP");
            Func<object, object> getter;
            if (PropertyGetterCache.TryGetValue(key, out getter)) return getter;

            PropertyInfo pi = GetPropertyCached(type, propertyName, flags);
            if (pi == null || !pi.CanRead)
            {
                getter = delegate (object _) { return null; };
                PropertyGetterCache[key] = getter;
                return getter;
            }

            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            UnaryExpression castInstance = Expression.Convert(instanceParam, type);
            MemberExpression propAccess = Expression.Property(castInstance, pi);
            UnaryExpression convertResult = Expression.Convert(propAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(convertResult, instanceParam);
            getter = lambda.Compile();
            PropertyGetterCache[key] = getter;
            return getter;
        }

        public static Action<object, object> CreatePropertySetter(Type type, string propertyName,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string key = KeyFor(type, propertyName, "SETPROP");
            Action<object, object> setter;
            if (PropertySetterCache.TryGetValue(key, out setter)) return setter;

            PropertyInfo pi = GetPropertyCached(type, propertyName, flags);
            if (pi == null || !pi.CanWrite)
            {
                setter = delegate (object _, object __) { };
                PropertySetterCache[key] = setter;
                return setter;
            }

            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");
            UnaryExpression castInstance = Expression.Convert(instanceParam, type);
            UnaryExpression castValue = Expression.Convert(valueParam, pi.PropertyType);
            MemberExpression propAccess = Expression.Property(castInstance, pi);
            BinaryExpression assign = Expression.Assign(propAccess, castValue);
            var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);
            setter = lambda.Compile();
            PropertySetterCache[key] = setter;
            return setter;
        }

        // ------------------ Method Invoker ------------------
        public static Func<object, object[], object> CreateMethodInvoker(Type type, string methodName,
            Type[] paramTypes = null,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        {
            string args = "";
            if (paramTypes != null)
            {
                List<string> names = new List<string>();
                foreach (var t in paramTypes) names.Add(t.FullName);
                args = string.Join(",", names.ToArray());
            }

            string key = KeyFor(type, methodName, "INVOKE_" + args);
            Func<object, object[], object> inv;
            if (MethodInvokerCache.TryGetValue(key, out inv)) return inv;

            MethodInfo mi = GetMethodCached(type, methodName, paramTypes, flags);
            if (mi == null)
            {
                inv = delegate (object _, object[] __) { return null; };
                MethodInvokerCache[key] = inv;
                return inv;
            }

            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");
            ParameterInfo[] parameters = mi.GetParameters();
            Expression[] paramExps = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ConstantExpression index = Expression.Constant(i);
                BinaryExpression paramAccessor = Expression.ArrayIndex(argsParam, index);
                UnaryExpression convertParam = Expression.Convert(paramAccessor, parameters[i].ParameterType);
                paramExps[i] = convertParam;
            }

            Expression callInstance = mi.IsStatic ? null : Expression.Convert(instanceParam, type);
            MethodCallExpression call = Expression.Call(callInstance, mi, paramExps);
            Expression body;
            if (mi.ReturnType == typeof(void))
            {
                body = Expression.Block(call, Expression.Constant(null, typeof(object)));
            }
            else
            {
                body = Expression.Convert(call, typeof(object));
            }

            var lambda = Expression.Lambda<Func<object, object[], object>>(body, instanceParam, argsParam);
            inv = lambda.Compile();
            MethodInvokerCache[key] = inv;
            return inv;
        }
    }
}
