using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal class LocalVariableTranspiler {

		private readonly CilBody _body;
		private readonly DMContext _ctx;

		private Dictionary<Local, Local> _localBuilderMapping = new Dictionary<Local, Local>();
		public LocalVariableTranspiler(CilBody body, DMContext ctx) {
			_body = body;
			_ctx = ctx;
		}

		public Local GetLocal(Local local) => _localBuilderMapping[local];

		public void MapLocals() {
			foreach(var variable in _body.Variables) {
				var localBuilderLocal = new Local(_ctx.LocalBuilderTypeSig);
				localBuilderLocal.Name = variable.Name;

				_localBuilderMapping.Add(variable, localBuilderLocal);
			}
		}

		public void AddLocalsToBody() {
			foreach(var local in _localBuilderMapping.Values) {
				_body.Variables.Add(local);
			}
		}


		public void EmitLocalDeclaration(IList<Instruction> target) {
			foreach(var entry in _localBuilderMapping) {
				var old = entry.Key;
				var @new = entry.Value;

				target.Add(OpCodes.Ldloc.ToInstruction(ILGenerator));

				var type = old.Type.ToTypeDefOrRef();

				target.Add(OpCodes.Ldtoken.ToInstruction(type));
				target.Add(OpCodes.Call.ToInstruction(_ctx.TypeOf));
				target.Add(OpCodes.Callvirt.ToInstruction(_ctx.DeclareLocal));
				target.Add(OpCodes.Stloc.ToInstruction(@new));
			}
		}

		public Local ILGenerator {
			get;
			set;
		}
	}
}
