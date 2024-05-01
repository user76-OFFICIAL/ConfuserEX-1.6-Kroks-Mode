using Confuser.Core;

namespace Confuser.Protections.DynamicMethods {

	[BeforeProtection("Ki.RefProxy")]

	internal class DynamicMethodsProtection : Protection {
		public override ProtectionPreset Preset => ProtectionPreset.Maximum;

		public override string Name => "Dynamic Methods";

		public override string Description => "Transforms all method bodies to dynamic methods.";

		public override string Id => "dynamic methods";

		public override string FullId => "Ki.DynamicMethods";

		internal static readonly object ContextKey = new object();

		protected override void Initialize(ConfuserContext context) {
			//
		}
	
		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new InjectPhase(this));
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new TranspilationPhase(this));
		}

	
	}
}
