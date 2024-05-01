using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineNoneTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineNone,
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineNone));
			return TranspilationResult.SuccessInstance;
		}
	}
}
