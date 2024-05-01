
using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineBrTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.ShortInlineBrTarget,
			OperandType.InlineBrTarget
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {

			if (!(context.Current.Operand is Instruction instr)) {
				return TranspilationResult.Failed("Not a instruction operand.");
			}

			var label = context.BranchTranspiler.GetLabel(instr);
			context.Emit(OpCodes.Ldloc.ToInstruction(label));
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineBr));
			return TranspilationResult.SuccessInstance;
		}
	}
}
