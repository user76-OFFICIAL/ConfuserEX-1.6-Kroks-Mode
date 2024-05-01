using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineMethodTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineMethod,
		};


		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if(!(context.Current.Operand is IMethod iMethod)) {
				return TranspilationResult.Failed("Not a method operand.");
			}

			var method = iMethod.ResolveMethodDef();

			if(method is null) {
				return TranspilationResult.Failed($"Failed to resolve method: {iMethod.FullName}");
			}

			context.Emit(Instruction.CreateLdcI4(iMethod.MDToken.ToInt32()));

			if(iMethod is MethodSpec spec) {
				var types = spec.GenericInstMethodSig.GetGenericArguments();
				DynamicMethodsUtils.EmitTypeArray(context, types);
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetGenericMethod));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineMethod));
			}
			else if (method.IsConstructor) {
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetConstructor));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineConstructor));
			}
			else {
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetMethod));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineMethod));
			}

			return TranspilationResult.SuccessInstance;
		}
	}
}
