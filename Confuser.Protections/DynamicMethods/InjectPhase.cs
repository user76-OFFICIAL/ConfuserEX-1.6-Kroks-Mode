using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal class InjectPhase : ProtectionPhase {
		public InjectPhase(DynamicMethodsProtection parent) : base(parent) {

		}
		public override ProtectionTargets Targets => ProtectionTargets.Modules;

		public override string Name => "Inject phase.";



		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {

			var corLibTypes = context.CurrentModule.CorLibTypes;
			const string Sre = "System.Reflection.Emit";

			var dynamicMethodTypeRef = corLibTypes.GetTypeRef(Sre, "DynamicMethod");
			var ilGeneratorTypeRef = corLibTypes.GetTypeRef(Sre, "ILGenerator");

			var ctx = new DMContext() {
				CurrentModule = context.CurrentModule,
				DynamicMethodTypeSig = dynamicMethodTypeRef.ToTypeSig(),
				ILGeneratorTypeSig = ilGeneratorTypeRef.ToTypeSig(),
				LocalBuilderTypeSig = corLibTypes.GetTypeRef(Sre, "LocalBuilder").ToTypeSig(),
				LabelTypeSig = corLibTypes.GetTypeRef(Sre, "Label").ToTypeSig(),
				OpCodeTypeSig = corLibTypes.GetTypeRef(Sre, "OpCode").ToTypeSig(),
			};

			ctx.GetILGenerator = new MemberRefUser(context.CurrentModule, "GetILGenerator",
							MethodSig.CreateInstance(ctx.ILGeneratorTypeSig),
							dynamicMethodTypeRef);

			ctx.DeclareLocal = new MemberRefUser(context.CurrentModule, "DeclareLocal",
										MethodSig.CreateInstance(ctx.LocalBuilderTypeSig, corLibTypes.GetTypeRef("System", "Type").ToTypeSig()),
										ilGeneratorTypeRef);

			ctx.DefineLabel = new MemberRefUser(context.CurrentModule, "DefineLabel",
									MethodSig.CreateInstance(ctx.LabelTypeSig),
									ilGeneratorTypeRef);

			ctx.MarkLabel = new MemberRefUser(context.CurrentModule, "MarkLabel",
											MethodSig.CreateInstance(corLibTypes.Void, ctx.LabelTypeSig),
											ilGeneratorTypeRef);

			ctx.EmitInlineNone = new MemberRefUser(context.CurrentModule, "Emit",
											MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig),
											ilGeneratorTypeRef);

			ctx.EmitInlineString = new MemberRefUser(context.CurrentModule, "Emit",
									MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.String),
									ilGeneratorTypeRef);

			ctx.EmitInlineI = new MemberRefUser(context.CurrentModule, "Emit",
								MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.Int32),
								ilGeneratorTypeRef);


			ctx.EmitInlineI8 = new MemberRefUser(context.CurrentModule, "Emit",
								MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.Int64),
								ilGeneratorTypeRef);

			ctx.EmitInlineR = new MemberRefUser(context.CurrentModule, "Emit",
							MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.Double),
							ilGeneratorTypeRef);


			ctx.EmitInlineVar = new MemberRefUser(context.CurrentModule, "Emit",
									MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, ctx.LocalBuilderTypeSig),
									ilGeneratorTypeRef);

			ctx.EmitInlineBr = new MemberRefUser(context.CurrentModule, "Emit",
									MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, ctx.LabelTypeSig),
									ilGeneratorTypeRef);


			ctx.EmitInlineBr = new MemberRefUser(context.CurrentModule, "Emit",
									MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, ctx.LabelTypeSig),
									ilGeneratorTypeRef);

			ctx.EmitInlineType = new MemberRefUser(context.CurrentModule, "Emit",
									MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.GetTypeRef("System", "Type").ToTypeSig()),
									ilGeneratorTypeRef);


			ctx.EmitInlineMethod = new MemberRefUser(context.CurrentModule, "Emit",
								MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.GetTypeRef("System.Reflection", "MethodInfo").ToTypeSig()),
								ilGeneratorTypeRef);


			ctx.EmitInlineConstructor = new MemberRefUser(context.CurrentModule, "Emit",
								MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.GetTypeRef("System.Reflection", "ConstructorInfo").ToTypeSig()),
								ilGeneratorTypeRef);

			ctx.EmitInlineField = new MemberRefUser(context.CurrentModule, "Emit",
								MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.GetTypeRef("System.Reflection", "FieldInfo").ToTypeSig()),
								ilGeneratorTypeRef);


			ctx.EmitInlineField = new MemberRefUser(context.CurrentModule, "Emit",
								MethodSig.CreateInstance(corLibTypes.Void, ctx.OpCodeTypeSig, corLibTypes.GetTypeRef("System.Reflection", "FieldInfo").ToTypeSig()),
								ilGeneratorTypeRef);


			ctx.Invoke = new MemberRefUser(context.CurrentModule, "Invoke",
								MethodSig.CreateInstance(corLibTypes.Object, corLibTypes.Object, new SZArraySig(corLibTypes.Object)),
								corLibTypes.GetTypeRef("System.Reflection", "MethodBase"
								));

			TypeDef rtType = context.Registry.GetService<IRuntimeService>().GetRuntimeType("Confuser.Runtime.DynamicMethods");

			var marker = context.Registry.GetService<IMarkerService>();
			var name = context.Registry.GetService<INameService>();

			foreach (ModuleDef module in parameters.Targets.OfType<ModuleDef>()) {
				IEnumerable<IDnlibDef> members = InjectHelper.Inject(rtType, module.GlobalType, module);


				foreach (IDnlibDef member in members) {
					if (member is MethodDef method) {
						if (method.Name == "Create")
							ctx.CreateDynamicMethod = method;
						else if (method.Name == "TypeOf")
							ctx.TypeOf = method;
						else if (method.Name == "GetOpCode")
							ctx.GetOpCode = method;
						else if (method.Name == "GetType")
							ctx.GetTypeMethod = method;
						else if (method.Name == "GetGenericType")
							ctx.GetGenericType = method;
						else if (method.Name == "GetMethodInfo")
							ctx.GetMethod = method;
						else if (method.Name == "GetGenericMethodInfo")
							ctx.GetGenericMethod = method;
						else if (method.Name == "GetConstructorInfo")
							ctx.GetConstructor = method;
						else if (method.Name == "GetField")
							ctx.GetField = method;
						else if (method.Name == "GetCached")
							ctx.GetCached = method;
						else if (method.Name == "AddToCache")
							ctx.AddToCache = method;
						else if (method.Name == "Initialize")
							module.GlobalType.FindOrCreateStaticConstructor().Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(method));

						name.SetCanRename(member, false);
						name.MarkHelper(member, marker, (Protection)Parent);
				
						ProtectionParameters.GetParameters(context, method).Remove(Parent);
					}
				}
			}

			context.CurrentModuleWriterOptions.MetadataOptions.Flags |= dnlib.DotNet.Writer.MetadataFlags.PreserveRids;
			context.Annotations.Set(context.CurrentModule, DynamicMethodsProtection.ContextKey, ctx);
		}
	}
}
