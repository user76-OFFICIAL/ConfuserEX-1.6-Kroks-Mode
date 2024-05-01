using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ConstantsMutation.Mutations {
	internal abstract class ConstantMutation {
		public ConstantMutation(ModuleDef module) {
			Initialise(module);
		}
		protected abstract void Initialise(ModuleDef module);
		public abstract void Mutate(IList<Instruction> instructions, int index);
	}
}
