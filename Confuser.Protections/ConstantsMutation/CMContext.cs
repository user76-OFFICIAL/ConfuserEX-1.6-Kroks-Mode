
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;

namespace Confuser.Protections.ConstantsMutation {
	internal class CMContext {

		public ConfuserContext ConfuserContext {
			get;
			set;
		}
		public RandomGenerator Random {
			get;
			set;
		}

		public IDynCipherService DynCipher {
			get;
			set;
		}

		public INameService Name {
			get;
			set;
		}

		public IMarkerService Marker {
			get;
			set;
		}

		public MethodDef Add {
			get;
			set;
		}

		
	}
}
