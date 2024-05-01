using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineITranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineI,
			OperandType.ShortInlineI
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if(!context.Current.IsLdcI4()) {
				return TranspilationResult.Failed("Not a ldc.i4 instruction.");
			}

			context.Emit(Instruction.CreateLdcI4(context.Current.GetLdcI4Value()));
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineI));
			return TranspilationResult.SuccessInstance;
		}
	}
}
