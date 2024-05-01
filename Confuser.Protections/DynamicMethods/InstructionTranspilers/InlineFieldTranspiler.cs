using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineFieldTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineField,
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if (!(context.Current.Operand is IField type)) {
				return TranspilationResult.Failed("Not a field operand.");
			}


			context.Emit(Instruction.CreateLdcI4(type.MDToken.ToInt32()));
			context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetField));
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineField));
			return TranspilationResult.SuccessInstance;
		}
	}
}
