using System.Collections.Generic;
using System.Linq;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Generation {
	internal class StatementGenerator {


		static LoopStatement GenerateInverse(LoopStatement encodeLoop, Expression var, Dictionary<AssignmentStatement, (Expression encode, Expression inverse)> assignments) {
			var decodeLoop = new LoopStatement() {
				Begin = encodeLoop.Begin,
				Limit = encodeLoop.Limit,
			};

			foreach(var assignment in assignments.Reverse()) {
				decodeLoop.Statements.Add(new AssignmentStatement {
					Target = var,
					Value = assignment.Value.inverse
				});
			}

			return decodeLoop;
		}

		public static void GeneratePair(RandomGenerator random, Expression var, Expression result, int depth, out LoopStatement statement, out LoopStatement inverse) {
		
			statement = new LoopStatement {
				Begin = 0,
				Limit = depth,
			};

			var assignments = new Dictionary<AssignmentStatement, (Expression encode, Expression inverse)>();

			for(int i = 0; i < depth; i++) {
				ExpressionGenerator.GeneratePair(random, var, result, depth, out var expression, out var inverseExpression);

				var assignment = new AssignmentStatement {
					Target = var,
					Value = expression
				};

				assignments.Add(assignment, (expression, inverseExpression));
				statement.Statements.Add(assignment);
			}

			inverse = GenerateInverse(statement, result, assignments);
		}
	}
}
