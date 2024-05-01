using System;
using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineStringTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineString
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if(!(context.Current.Operand is string)) {
				return TranspilationResult.Failed("Not a string operand.");
			}
			context.Emit(OpCodes.Ldstr.ToInstruction((string)context.Current.Operand));
			context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineString)); 
			return TranspilationResult.SuccessInstance;
		}
	}
}
