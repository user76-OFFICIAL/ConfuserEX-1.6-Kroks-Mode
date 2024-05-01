using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal class BranchTranspiler {

		private readonly CilBody _body;
		private readonly DMContext _ctx;
		private HashSet<Instruction> _branchTargets = new HashSet<Instruction>();
		private Dictionary<Instruction, Local> _labelMapping = new Dictionary<Instruction, Local>();
		public BranchTranspiler(CilBody body, DMContext ctx) {
			_body = body;
			_ctx = ctx;
		}


		public bool IsTarget(Instruction instruction) {
			return _branchTargets.Contains(instruction);
		}

		public Local GetLabel(Instruction instruction) {
			return _labelMapping[instruction];
		}

		public void CollectBranchTargets() {
			foreach(var instruction in _body.Instructions) {

				if (instruction.OpCode.OperandType != OperandType.InlineBrTarget)
					continue;

				_branchTargets.Add((Instruction)instruction.Operand);
			}
		}


		public void EmitLabelDeclaration(IList<Instruction> target) {
			foreach(var branchTarget in _branchTargets) {
				var labelLocal = new Local(_ctx.LabelTypeSig);
				_body.Variables.Add(labelLocal);

				target.Add(OpCodes.Ldloc.ToInstruction(ILGenerator));
				target.Add(OpCodes.Callvirt.ToInstruction(_ctx.DefineLabel));
				target.Add(OpCodes.Stloc.ToInstruction(labelLocal));

				_labelMapping.Add(branchTarget, labelLocal);
			}
		}


		public Local ILGenerator {
			get;
			set;
		} 

	}
}
