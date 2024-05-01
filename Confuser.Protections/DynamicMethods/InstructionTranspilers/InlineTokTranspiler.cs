using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InlineTokTranspiler : IInstructionTranspiler {
		public IReadOnlyCollection<OperandType> OperandTypes => new[] {
			OperandType.InlineTok,
		};

		public TranspilationResult Transpile(InstructionTranspilationContext context) {
			if(context.Current.Operand is IType type) {
				context.Emit(Instruction.CreateLdcI4(type.MDToken.ToInt32()));
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetTypeMethod));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineType));
			}
			else if(context.Current.Operand is IMethod method) {
				context.Emit(Instruction.CreateLdcI4(method.MDToken.ToInt32()));
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetMethod));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineMethod));
			}
			else if (context.Current.Operand is IField field) {
				context.Emit(Instruction.CreateLdcI4(field.MDToken.ToInt32()));
				context.Emit(OpCodes.Call.ToInstruction(context.Ctx.GetField));
				context.Emit(OpCodes.Callvirt.ToInstruction(context.Ctx.EmitInlineField));
			}
			else {
				return TranspilationResult.Failed("Not a member operand.");
			}

			return TranspilationResult.SuccessInstance;
		}
	}
}
