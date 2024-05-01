using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Protections.DynamicMethods.InstructionTranspilers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal class MethodBodyContentTranspiler {

		private readonly MethodDef _method;
		private readonly DMContext _ctx;
		private readonly LocalVariableTranspiler _localTranspiler;
		private readonly BranchTranspiler _branchTranspiler;

		private static readonly IInstructionTranspiler[] _instructionTranspilers = new IInstructionTranspiler[] {
			new InlineNoneTranspiler(),
			new InlineBrTranspiler(),
			new InlineFieldTranspiler(),
			new InlineI8Transpiler(),
			new InlineITranspiler(),
			new InlineRTranspiler(),
			new InlineStringTranspiler(),
			new InlineMethodTranspiler(),
			new InlineTypeTranspiler(),
			new InlineVarTranspiler(),
			new InlineTokTranspiler(),
		};

		private static Dictionary<OperandType, IInstructionTranspiler> _transpilerMapping = new Dictionary<OperandType, IInstructionTranspiler>();

		static MethodBodyContentTranspiler() {
			foreach(var transpiler in _instructionTranspilers) {
				foreach(var operandType in transpiler.OperandTypes) {
					_transpilerMapping.Add(operandType, transpiler);
				}
			}
		}

		public MethodBodyContentTranspiler(MethodDef method, DMContext ctx, LocalVariableTranspiler localTranspiler, BranchTranspiler branchTranspiler) {
			_method = method;
			_ctx = ctx;
			_localTranspiler = localTranspiler;
			_branchTranspiler = branchTranspiler;
		}

		public void Transpile(IList<Instruction> target) {
			Debug.Assert(ILGenerator != null);

			var transpilationContext = new InstructionTranspilationContext {
				Method = _method,
				LocalTranspiler = _localTranspiler,
				BranchTranspiler = _branchTranspiler,
				Target = target,
				Ctx = _ctx,
			};

			foreach (var instruction in _method.Body.Instructions) {

				transpilationContext.Current = instruction;

				if (_branchTranspiler.IsTarget(instruction)) {
					var branchLocal = _branchTranspiler.GetLabel(instruction);
					target.Add(OpCodes.Ldloc.ToInstruction(ILGenerator));
					target.Add(OpCodes.Ldloc.ToInstruction(branchLocal));
					target.Add(OpCodes.Callvirt.ToInstruction(_ctx.MarkLabel));
				}

				target.Add(OpCodes.Ldloc.ToInstruction(ILGenerator));
				target.Add(Instruction.CreateLdcI4(instruction.OpCode.Value));
				target.Add(OpCodes.Call.ToInstruction(_ctx.GetOpCode));


				var operandType = instruction.OpCode.OperandType;

				if(!_transpilerMapping.TryGetValue(operandType, out var transpiler)) {
					throw new TranspilationException($"Handler for operandtype {operandType.ToString()} not found.");
				}

				var result = transpiler.Transpile(transpilationContext);
				if (!result.Success)
					throw new TranspilationException(result.Message);
			}
		}


		public Local ILGenerator {
			get;
			set;
		}


	}
}
