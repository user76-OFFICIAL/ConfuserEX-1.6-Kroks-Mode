using System;
using Confuser.Core;

namespace Confuser.Protections.ConstantsMutation {
	[BeforeProtection("Ki.Constants")]
	internal class ConstantsMutationProtection : Protection {
		public override ProtectionPreset Preset => ProtectionPreset.Normal;

		public override string Name => "Contants Mutation";

		public override string Description => "Mutates constants";

		public override string Id => "constantmutation";

		public override string FullId => "Ki.Mutation";

		internal static readonly object ContextKey = new object();

		protected override void Initialize(ConfuserContext context) {
			//
		}
		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.ProcessModule, new MutationPhase(this));
		}
	}
}
