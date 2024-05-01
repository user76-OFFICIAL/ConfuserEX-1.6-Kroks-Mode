
namespace Confuser.Protections.DynamicMethods.InstructionTranspilers {
	internal class TranspilationResult {

		public static TranspilationResult SuccessInstance = new TranspilationResult {
			Success = true,
		};

		public static TranspilationResult Failed(string message) => new TranspilationResult {
			Success = false,
			Message = message
		};

		public bool Success {
			get;
			set;
		}

		public string Message {
			get;
			set;
		}
	}
}
