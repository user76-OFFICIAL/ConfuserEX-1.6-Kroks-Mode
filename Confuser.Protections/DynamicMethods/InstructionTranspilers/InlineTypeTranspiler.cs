using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineTypeTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineType,
		};
		private void EmitTypeArray(InstructionTranspilationContext context, IList<TypeSig> types) {
			int n = types.Count;
			context.Emit(Instruction.CreateLdcI4(n));
			context.Emit(OpCodes.Newarr.ToInstruction(context.Ctx.CurrentModule.CorLibTypes.GetTypeRef("System", "Type")));
			for (int i = 0; i < n; i++) {
				context.Emit(OpCodes.Dup.ToInstruction());
				context.Emit(Instruction.CreateLdcI4(i));
				context.Emit(OpCodes.Ldtoken.ToInstruction(types[i].ToTypeDefOrRef()));
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.TypeOf));
				context.Emit(OpCodes.Stelem_Ref.ToInstruction());
			}
		}

		public TranspilationResult Transpile(InstructionTranspilationContext context) {

			if(!(context.Current.Operand is IType iType)) {
				return TranspilationResult.Failed("Not a type operand.");
			}

			context.Emit(Instruction.CreateLdcI4(iType.MDToken.ToInt32()));

			if (iType is TypeSpec spec) {
			    var genericInstSig = spec.TryGetGenericInstSig();

				if (genericInstSig is null)
					return TranspilationResult.Failed("Could not resolve generic inst sig.");

				EmitTypeArray(context, genericInstSig.GenericArguments);
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetGenericType));
			}
			else {
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetTypeMethod));
			}

			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineType));
			return TranspilationResult.SuccessInstance;
		}
	}
}
