using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineRTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineR,
			OperandType.ShortInlineR
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if (!(context.Current.Operand is double value)) {
				return TranspilationResult.Failed("Not a double operand.");
			}

			context.Emit(OpCodes.Ldc_R8.ToInstruction(value));
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineR));
			return TranspilationResult.SuccessInstance;
		}
	}
}
