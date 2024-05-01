using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineVarTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineVar,
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {

			if (context.Current.Operand is Local targetLocal) {
				var local = context.LocalTranspiler.GetLocal(targetLocal);
				context.Emit(OpCodes.Ldloc.ToInstruction(local));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineVar));
			}
			else if(context.Current.Operand is Parameter targetParameter) {
				//Ldarg
				context.Emit(Instruction.CreateLdcI4(context.Method.Parameters.IndexOf(targetParameter)));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineI));
			}
			else {
				return TranspilationResult.Failed("Not a variable operand");
			}

			return TranspilationResult.SuccessInstance;
		}
	}
}
