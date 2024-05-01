using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal interface IInstructionTranspiler {
		IReadOnlyCollection<OperandType> OperandTypes {
			get;
		}
		TranspilationResult Transpile(InstructionTranspilationContext context);
	}
}
