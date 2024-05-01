using System.Collections.Generic;
using Confuser.Protections.DynamicMethods.InstructionTranspilers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal static class DynamicMethodsUtils {
		internal static void EmitTypeArray(InstructionTranspilationContext context, IList<TypeSig> types) {
			int n = types.Count;
			context.Emit(Instruction.CreateLdcI4(n));
			context.Emit(OpCodes.Newarr.ToInstruction(context.Ctx.CurrentModule.CorLibTypes.GetTypeRef("System", "Type")));
			for (int i = 0; i < n; i++) {
				context.Emit(OpCodes.Dup.ToInstruction());
				context.Emit(Instruction.CreateLdcI4(i));
				context.Emit(OpCodes.Ldtoken.ToInstruction(types[i].TryGetTypeDefOrRef()));
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.TypeOf));
				context.Emit(OpCodes.Stelem_Ref.ToInstruction());
			}
		}

		internal static ITypeDefOrRef GetTypeDeclaration(ITypeDefOrRef type) {

			if(type.NumberOfGenericParameters > 0) {
				int n = type.NumberOfGenericParameters;
				var sig = type.TryGetClassOrValueTypeSig();

				var genericVars = new GenericVar[n];

				for (int i = 0; i < n; i++)
					genericVars[i] = new GenericVar(i);

				var genericSig = new GenericInstSig(sig, genericVars);

				var spec = new TypeSpecUser(genericSig);

				return spec;
			}

			return type;
		}
	}
}
