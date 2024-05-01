using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Protections.ControlFlow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal class TranspilationPhase : ProtectionPhase {
		public TranspilationPhase(DynamicMethodsProtection parent) : base(parent) {

		}
		public override ProtectionTargets Targets => ProtectionTargets.Methods;

		public override string Name => "transpilation phase.";


		private void Initialize(MethodDef method, IList<Instruction> target, DMContext ctx, Local dynamicMethod, Local ilGenerator) {
			target.Add(Instruction.CreateLdcI4(method.MDToken.ToInt32()));
			target.Add(OpCodes.Call.ToInstruction(ctx.CreateDynamicMethod));
			target.Add(OpCodes.Stloc.ToInstruction(dynamicMethod));
			target.Add(OpCodes.Ldloc.ToInstruction(dynamicMethod));
			target.Add(OpCodes.Callvirt.ToInstruction(ctx.GetILGenerator));
			target.Add(OpCodes.Stloc.ToInstruction(ilGenerator));
		}


		private void EmitInvocation(MethodDef method, IList<Instruction> target, Local dynamicMethod, DMContext ctx) {
			int n = method.Parameters.Count;
			target.Add(OpCodes.Ldloc.ToInstruction(dynamicMethod));
			if (method.IsStatic) {
				target.Add(OpCodes.Ldnull.ToInstruction());
			}
			else {
				target.Add(OpCodes.Ldarg_0.ToInstruction());

				if (method.DeclaringType.IsValueType) {
					var typeSpec = DynamicMethodsUtils.GetTypeDeclaration(method.DeclaringType);
					target.Add(OpCodes.Ldobj.ToInstruction(typeSpec));
					target.Add(OpCodes.Box.ToInstruction(typeSpec));
				}
			}
			target.Add(Instruction.CreateLdcI4(n));
			target.Add(OpCodes.Newarr.ToInstruction(ctx.CurrentModule.CorLibTypes.Object));

			for (int i = 0; i < n; i++) {
				var parameter = method.Parameters[i];

				target.Add(OpCodes.Dup.ToInstruction());
				target.Add(Instruction.CreateLdcI4(i));
				target.Add(OpCodes.Ldarg.ToInstruction(method.Parameters[i]));

				if(parameter.IsHiddenThisParameter && method.DeclaringType.IsValueType) {
					var spec = DynamicMethodsUtils.GetTypeDeclaration(method.DeclaringType);
					target.Add(OpCodes.Ldobj.ToInstruction(spec));
					target.Add(OpCodes.Box.ToInstruction(spec));
				}
	
				target.Add(OpCodes.Stelem_Ref.ToInstruction());
			}

			target.Add(OpCodes.Callvirt.ToInstruction(ctx.Invoke));

		}


		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var ctx = context.Annotations.Get<DMContext>(context.CurrentModule, DynamicMethodsProtection.ContextKey);

			foreach(var method in parameters.Targets.OfType<MethodDef>()) {
				if (!method.HasBody || method.IsConstructor || method.IsStaticConstructor || method.Body.HasExceptionHandlers || method.Module.GlobalType == method.DeclaringType)
					continue;

				if (!method.IsEntryPoint())
					continue;

	
				//Generating a new identifier for each dynamic method.
				string identifier = Guid.NewGuid().ToString();

				var localTranspiler = new LocalVariableTranspiler(method.Body, ctx);

				//Mapping old locals to new locals which will be used in the generated dynamic method.
				localTranspiler.MapLocals();
				//Adding all new locals to the body.
				localTranspiler.AddLocalsToBody();

				var @new = new List<Instruction>();

				var dynamicMethod = new Local(ctx.DynamicMethodTypeSig);
				method.Body.Variables.Add(dynamicMethod);

				var ilGenerator = new Local(ctx.ILGeneratorTypeSig);
				method.Body.Variables.Add(ilGenerator);


				Instruction invocationBeginLabel = OpCodes.Nop.ToInstruction();
				//Check if the dynamic method already got created, if so we need to jump to the invocation
				@new.Add(OpCodes.Ldstr.ToInstruction(identifier));
				@new.Add(OpCodes.Call.ToInstruction(ctx.GetCached));
				@new.Add(OpCodes.Stloc.ToInstruction(dynamicMethod));
				@new.Add(OpCodes.Ldloc.ToInstruction(dynamicMethod));
				@new.Add(OpCodes.Ldnull.ToInstruction());
				@new.Add(OpCodes.Ceq.ToInstruction());
				@new.Add(OpCodes.Brfalse.ToInstruction(invocationBeginLabel));

			    Initialize(method, @new, ctx, dynamicMethod, ilGenerator); 

				localTranspiler.ILGenerator = ilGenerator;

				//Emitting the declaration of the new dynamic method locals.
				localTranspiler.EmitLocalDeclaration(@new);

				var branchTranspiler = new BranchTranspiler(method.Body, ctx) {
					ILGenerator = ilGenerator
				};

				//Find all branch targets
				branchTranspiler.CollectBranchTargets();
				//Emitting the declaration of the new dynamic method labels
				branchTranspiler.EmitLabelDeclaration(@new);

				//Since we are now finished with the needed initialization of branches and locals we can start to transpile the method body content.
				var contentTranspiler = new MethodBodyContentTranspiler(
					method,
					ctx,
					localTranspiler,
					branchTranspiler) {
					ILGenerator = ilGenerator
				};

				contentTranspiler.Transpile(@new);

				@new.Add(OpCodes.Ldstr.ToInstruction(identifier)); 
				@new.Add(OpCodes.Ldloc.ToInstruction(dynamicMethod));
				@new.Add(OpCodes.Call.ToInstruction(ctx.AddToCache));

				@new.Add(invocationBeginLabel);
				EmitInvocation(method, @new, dynamicMethod, ctx);
				if(!method.HasReturnType) {
					@new.Add(OpCodes.Pop.ToInstruction());
				}
				else {
					var retType = method.ReturnType.ToTypeDefOrRef();
					if (method.ReturnType.IsValueType) {
						@new.Add(OpCodes.Unbox_Any.ToInstruction(retType));
					}
					else {
						@new.Add(OpCodes.Castclass.ToInstruction(retType));
					}
				}

				@new.Add(OpCodes.Ret.ToInstruction());

				method.Body.Instructions.Clear();
				method.Body.Instructions.AddRange(@new);
			}
		}
	}
}
