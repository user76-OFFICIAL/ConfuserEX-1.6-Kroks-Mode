using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineI8Transpiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineI8,
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if (!(context.Current.Operand is long value)) {
				return TranspilationResult.Failed("Not a long operand.");
			}

			context.Emit(OpCodes.Ldc_I8.ToInstruction(value));
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineI8));
			return TranspilationResult.SuccessInstance;
		}
	}
}
