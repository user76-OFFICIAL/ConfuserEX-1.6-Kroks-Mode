using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Confuser.Runtime {
	internal static class DynamicMethods {

		private static Dictionary<short, OpCode> _opCodes;
		private static Dictionary<string, DynamicMethod> _cache;

		internal static void Initialize() {
			_opCodes = new Dictionary<short, OpCode>();
			_cache = new Dictionary<string, DynamicMethod>();

			foreach(var opCodeField in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) {
				var value = (OpCode)opCodeField.GetValue(null);
				_opCodes.Add(value.Value, value);
			}
		}

		internal static DynamicMethod GetCached(string name) {
			if (!_cache.TryGetValue(name, out var cached))
				return null;

			return cached;
		}

		internal static void AddToCache(string name, DynamicMethod method) {
			_cache[name] = method;
		}

		internal static OpCode GetOpCode(short value) {
			return _opCodes[value];
		}

		internal static Type TypeOf(RuntimeTypeHandle handle) {
			return Type.GetTypeFromHandle(handle);
		}


		internal static FieldInfo GetField(int mdToken) {
			var module = typeof(DynamicMethods).Module;
			var f = module.ResolveField(mdToken);
			var t = f.DeclaringType.IsGenericType ? f.DeclaringType.GetGenericTypeDefinition() : f.DeclaringType;
			var f2 = t.GetFields();
			FieldInfo x = null;
			for (int i = 0; i < f2.Length; i++) {
				if(f2[i].Name == f.Name)
					x = f2[i];
			}
			return x;
		}

		private static Type ResolveType(int mdToken) {
			var module = typeof(DynamicMethods).Module;
			return module.ResolveType(mdToken);
		}

		internal static Type GetGenericType(int mdToken, Type[] arguments) {
			var type = ResolveType(mdToken);
			return type.GetGenericTypeDefinition().MakeGenericType(arguments);
		}

		internal static Type GetType(int mdToken) {
			return ResolveType(mdToken);
		}

		private static MethodBase ResolveMethod(int mdToken) {
			var module = typeof(DynamicMethods).Module;
			return module.ResolveMethod(mdToken);
		} 

		internal static MethodInfo GetMethodInfo(int mdToken) {
			var method = ResolveMethod(mdToken);
			return (MethodInfo)method;
		}

		internal static MethodInfo GetGenericMethodInfo(int mdToken, Type[] types) {
			return GetMethodInfo(mdToken).GetGenericMethodDefinition().MakeGenericMethod(types);
		}

		internal static ConstructorInfo GetConstructorInfo(int mdToken) {
			var method = ResolveMethod(mdToken);
			return (ConstructorInfo)method;
		}

		internal static DynamicMethod Create(int mdToken) {
			MethodBase methodBase = ResolveMethod(mdToken);

			if (!(methodBase is MethodInfo method))
				throw new InvalidOperationException("Not a method.");

			var parameters = method.GetParameters();

			var parameterTypes = new List<Type>(parameters.Length);

			if(!method.IsStatic) {
				var typeInstance = method.DeclaringType;
				if(typeInstance.IsGenericType)
					typeInstance = typeInstance.GetGenericTypeDefinition();
				parameterTypes.Add(typeInstance);
			}
			for (int i = 0; i < parameters.Length; i++) {
				parameterTypes.Add(parameters[i].ParameterType);
			}

			//We cannot just use method.Attributes and method.CallingConventions, since a lot is not supported in dynamic methods. Only "public", "static" and "standard"

			MethodAttributes attributes = MethodAttributes.Public;
			attributes |= MethodAttributes.Static;

			var dynamicMethod = new DynamicMethod(method.Name, attributes, CallingConventions.Standard, method.ReturnType, parameterTypes.ToArray(), method.Module, true);
			return dynamicMethod;
		}
	
	}
}
