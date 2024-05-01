using dnlib.DotNet;

namespace Confuser.Protections.Constants {
	internal class DecoderInfo {
		public MethodDef Method {
			get;
			set;
		}

		public DecoderDesc DecoderDesc {
			get;
			set;
		}
	}
}
