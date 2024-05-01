using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class CEContext {
		public ConfuserContext Context {
			get;
			set;
		}
		public ConstantProtection Protection {
			get;
			set;
		}
		public ModuleDef Module {
			get;
			set;
		}
		public FieldDef BufferField {
			get;
			set;
		}
		public FieldDef DataField {
			get;
			set;
		}
		public TypeDef DataType {
			get;
			set;
		}
		public MethodDef InitMethod {
			get;
			set;
		}
		public int DecoderCount {
			get;
			set;
		}
		public List<DecoderInfo> Decoders {
			get;
			set;
		}

		public List<uint> EncodedBuffer {
			get;
			set;
		}
		public Mode Mode {
			get;
			set;
		}
		public IEncodeMode ModeHandler {
			get;
			set;
		}

		public IDynCipherService DynCipher {
			get;
			set;
		}
		public IMarkerService Marker {
			get;
			set;
		}
		public INameService Name {
			get;
			set;
		}
		public RandomGenerator Random {
			get;
			set;
		}

		public TypeDef CfgCtxType {
			get;
			set;
		}
		public MethodDef CfgCtxCtor {
			get;
			set;
		}
		public MethodDef CfgCtxNext {
			get;
			set;
		}
		public Dictionary<MethodDef, List<ReplaceableInstructionReference>> ReferenceRepl {
			get;
			set;
		}
	}

	internal class DecoderDesc {
		public object Data;
		public byte InitializerID;
		public byte NumberID;
		public byte StringID;
	}
}
