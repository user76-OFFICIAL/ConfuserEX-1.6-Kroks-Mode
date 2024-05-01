
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class InstructionTranspilationContext {

		public IList<Instruction> Target {
			get;
			set;
		}

		public MethodDef Method {
			get;
			set;
		}

		public Instruction Current {
			get;
			set;
		}

		public DMContext Ctx {
			get;
			set;
		}

		public LocalVariableTranspiler LocalTranspiler {
			get;
			set;
		}

		public BranchTranspiler BranchTranspiler {
			get;
			set;
		}

		public void Emit(Instruction instr) => Target.Add(instr);
	}
}
