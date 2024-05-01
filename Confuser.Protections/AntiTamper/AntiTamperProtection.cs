﻿using System;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Protections.AntiTamper;
using Confuser.Protections.ConstantsMutation;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Protections {
	public interface IAntiTamperService {
		void ExcludeMethod(ConfuserContext context, MethodDef method);
	}

	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
	internal class AntiTamperProtection : Protection, IAntiTamperService {
		public const string _Id = "anti tamper";
		public const string _FullId = "Ki.AntiTamper";
		public const string _ServiceId = "Ki.AntiTamper";
		static readonly object HandlerKey = new object();

		public override string Name {
			get { return "Anti Tamper Protection"; }
		}

		public override string Description {
			get { return "This protection ensures the integrity of application."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.Maximum; }
		}

		internal static readonly object MutationFieldContext = new object();

		protected override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_ServiceId, typeof(IAntiTamperService), this);
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.BeginModule, new ModuleWriterSetupPhase(this));
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new InjectPhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new MDPhase(this));
		}

		public void ExcludeMethod(ConfuserContext context, MethodDef method) {
			ProtectionParameters.GetParameters(context, method).Remove(this);
		}

		class ModuleWriterSetupPhase : ProtectionPhase {
			public ModuleWriterSetupPhase(AntiTamperProtection parent) : base(parent) { }

			/// <inheritdoc />
			public override ProtectionTargets Targets => ProtectionTargets.Methods;

			/// <inheritdoc />
			public override string Name => "Anti-tamper module writer preparation";

			/// <inheritdoc />
			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any()) return;

				if (context.CurrentModuleWriterOptions is NativeModuleWriterOptions nativeOptions) {
					context.RequestNative(false);
				}
			}
		}



		class InjectPhase : ProtectionPhase {
			public InjectPhase(AntiTamperProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Methods; }
			}

			public override string Name {
				get { return "Anti-tamper helpers injection"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any())
					return;

				Mode mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Anti);
				IModeHandler modeHandler;
				switch (mode) {
			
					case Mode.Anti:
						modeHandler = new AntiMode();
						break;
				
					default:
						throw new UnreachableException();
				}
				modeHandler.HandleInject((AntiTamperProtection)Parent, context, parameters);
				context.Annotations.Set(context.CurrentModule, HandlerKey, modeHandler);
			}
		}

		class MDPhase : ProtectionPhase {
			public MDPhase(AntiTamperProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Methods; }
			}

			public override string Name {
				get { return "Anti-tamper metadata preparation"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any())
					return;

				var modeHandler = context.Annotations.Get<IModeHandler>(context.CurrentModule, HandlerKey);
				modeHandler.HandleMD((AntiTamperProtection)Parent, context, parameters);
			}
		}

		enum Mode {
			Normal,
			Anti,
			JIT
		}
	}
}
