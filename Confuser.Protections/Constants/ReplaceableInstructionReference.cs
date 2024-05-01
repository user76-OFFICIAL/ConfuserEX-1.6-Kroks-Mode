using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class ReplaceableInstructionReference {
		public IMethod Decoder {
			get;
			set;
		}

		public Instruction Target {
			get;
			set;
		}

		public uint Id {
			get;
			set;
		}

		public uint Key {
			get;
			set;
		}
	}
}
