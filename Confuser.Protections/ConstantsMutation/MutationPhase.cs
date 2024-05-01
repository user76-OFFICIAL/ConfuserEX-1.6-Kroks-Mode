using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Protections.ConstantsMutation.Mutations;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ConstantsMutation {
	internal class MutationPhase : ProtectionPhase {

		public MutationPhase(ConstantsMutationProtection parent) : base(parent) {

		}
		public override ProtectionTargets Targets => ProtectionTargets.Methods;

		public override string Name => "Constant mutation";

		private List<(MethodDef decodeMethod, int xorKey)> _decodeMethods = new List<(MethodDef, int)>();

	    void GenerateMutationMethod(TypeDef type, CMContext ctx) {

			var corLibTypes = ctx.ConfuserContext.CurrentModule.CorLibTypes;

			var decode = new MethodDefUser(
				ctx.Name.RandomName(),
				MethodSig.CreateStatic(corLibTypes.Int32,
				corLibTypes.Int32)) {
				Body = new CilBody(),
				Attributes = MethodAttributes.Public | MethodAttributes.Static,
			};


			type.Methods.Add(decode); 

			ctx.Marker.Mark(decode, Parent);
			
			int xorKey = ctx.Random.NextInt32(0, int.MaxValue);

			var stateVar = new Local(corLibTypes.Int32);
			decode.Body.Variables.Add(stateVar);

			var instructions = decode.Body.Instructions;

			const int StatementMax = 4;
			const int ExpressionMax = 8;

			instructions.Add(OpCodes.Ldarg_0.ToInstruction());

			if (ctx.Random.NextBoolean()) {
				ctx.DynCipher.GenerateExpressionMutation(ctx.Random, decode, stateVar, instructions, xorKey, ctx.Random.NextInt32(1, ExpressionMax));
			}
			else {
				ctx.DynCipher.GenerateStatementMutation(ctx.Random, decode, stateVar, instructions, xorKey, ctx.Random.NextInt32(1, StatementMax));
			}


			instructions.Add(OpCodes.Xor.ToInstruction());
			instructions.Add(OpCodes.Ret.ToInstruction());

			_decodeMethods.Add((decode, xorKey));
		}


		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {

			var ctx = new CMContext {
				ConfuserContext = context,
				DynCipher = context.Registry.GetService<IDynCipherService>(),
				Random = context.Registry.GetService<IRandomService>().GetRandomGenerator(Parent.Id),
				Name = context.Registry.GetService<INameService>(),
				Marker = context.Registry.GetService<IMarkerService>(),
			};

			ConstantMutation[] mutations = new ConstantMutation[] {
				new SizeofMutation(context.CurrentModule),
			};


	 		foreach (var method in parameters.Targets.OfType<MethodDef>()) {
		
				if(!method.HasBody || method.DeclaringType.IsGlobalModuleType) {
					continue;
				}


				var instructions = method.Body.Instructions;


				var ldci4Instructions = instructions.Where(instr => instr.IsLdcI4()).ToList();

				if(ldci4Instructions.Count == 0) {
					continue;
				}

				int decodeMethodsCount = ctx.Random.NextInt32(1, 8);
				if(decodeMethodsCount > ldci4Instructions.Count)
					decodeMethodsCount = ldci4Instructions.Count;

				for (int i = 0; i < decodeMethodsCount; i++) {
					GenerateMutationMethod(context.CurrentModule.GlobalType, ctx);
				}

				var vars = method.Body.Variables.Where(x => !x.Type.IsValueType).ToList();
				bool hasVars = vars.Count > 0;

				foreach(var instruction in ldci4Instructions) {

					var decode = _decodeMethods[ctx.Random.NextInt32(0, decodeMethodsCount)];

					var valueLocal = new Local(method.Module.CorLibTypes.Int32);
					method.Body.Variables.Add(valueLocal);

					var statement = new List<Instruction>();

					bool insertDefault = false;

					if(hasVars && ctx.Random.NextBoolean()) {
						var variable = vars[ctx.Random.NextInt32(0, vars.Count - 1)];
						statement.Add(OpCodes.Ldloc.ToInstruction(variable));
						statement.Add(OpCodes.Ldnull.ToInstruction());
						insertDefault = true;
					}
					else {
						var comparerLocal = new Local(method.Module.CorLibTypes.Int32);
						method.Body.Variables.Add(comparerLocal);

						int rand = ctx.Random.NextInt32();

						instructions.Insert(0, Instruction.CreateLdcI4(rand));
						instructions.Insert(1, OpCodes.Stloc.ToInstruction(comparerLocal));

						statement.Add(OpCodes.Ldloc.ToInstruction(comparerLocal));

		
						var toMutate2 = Instruction.CreateLdcI4(rand ^ decode.xorKey);
						statement.Add(toMutate2);
						mutations[0].Mutate(statement, statement.IndexOf(toMutate2));

						statement.Add(OpCodes.Call.ToInstruction(decode.decodeMethod));

					}


					Instruction label = OpCodes.Nop.ToInstruction();
					Instruction label2 = OpCodes.Nop.ToInstruction();


					int value = instruction.GetLdcI4Value();


					Instruction setLocalStart = Instruction.CreateLdcI4(value ^ decode.xorKey);

					statement.Add(OpCodes.Beq.ToInstruction(setLocalStart));
					statement.Add(OpCodes.Br.ToInstruction(label));

					statement.Add(setLocalStart);
					statement.Add(OpCodes.Call.ToInstruction(decode.decodeMethod));

					statement.Add(OpCodes.Stloc.ToInstruction(valueLocal));
					statement.Add(OpCodes.Br.ToInstruction(label2));

					statement.Add(label);
					if(insertDefault) {
						var toMutate = Instruction.CreateLdcI4(value ^ decode.xorKey);
						statement.Add(toMutate);
						mutations[0].Mutate(statement, statement.IndexOf(toMutate));
						statement.Add(OpCodes.Call.ToInstruction(decode.decodeMethod));
						statement.Add(OpCodes.Stloc.ToInstruction(valueLocal));
					}

					statement.Add(label2);
					statement.Add(OpCodes.Ldloc.ToInstruction(valueLocal));

					instruction.ReplaceWithInstructionList(statement, method);
				}

			}
		}
	}
}
