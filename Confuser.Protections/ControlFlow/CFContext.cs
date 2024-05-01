using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal enum CFType {
		Switch,
		Jump
	}

	internal enum PredicateType {
		Normal,
		Expression,
		x86
	}

	internal class CFContext {
		public ConfuserContext Context {
			get;
			set;
		}
		public ControlFlowProtection Protection {
			get;
			set;
		}
		public int Depth {
			get;
			set;
		}
		public IDynCipherService DynCipher {
			get;
			set;
		}

		public double Intensity {
			get;
			set;
		}
		public bool JunkCode {
			get;
			set;
		}
		public MethodDef Method {
			get;
			set;
		}

		public Local StateVariable {
			get;
			set;
		}

		public PredicateType Predicate {
			get;
			set;
		}
		public RandomGenerator Random {
			get;
			set;
		}

		public CFType Type {
			get;
			set;
		}



		public void AddJump2(IList<Instruction> instructions, Instruction target) {
		
			int num1 = Random.NextInt32();
			int num2 = Random.NextInt32();
			int depth = Random.NextInt32(1, 5);

			DynCipher.GenerateExpressionMutation(Random, Method, StateVariable, instructions, num1, depth);
			DynCipher.GenerateExpressionMutation(Random, Method, StateVariable, instructions, num2, depth);

			OpCode comparer = null;
			if (num1 == num2)
				comparer = OpCodes.Beq;
			else if (num1 > num2)
				comparer = OpCodes.Bgt;
			else
				comparer = OpCodes.Blt;



			instructions.Add(Instruction.Create(comparer, target));
			//Handle the fallthrough
			//var exception = Method.Module.CorLibTypes.GetTypeRef("System", "Exception");
			//var ctor = new MemberRefUser(Method.Module, ".ctor", MethodSig.CreateInstance(Method.Module.CorLibTypes.Void), exception);
			//instructions.Add(OpCodes.Newobj.ToInstruction(ctor));
			instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			instructions.Add(OpCodes.Throw.ToInstruction());
			instructions.Add(OpCodes.Ret.ToInstruction());
		
		}
		public void AddJump(IList<Instruction> instrs, Instruction target) {
			instrs.Add(Instruction.Create(OpCodes.Br, target));
		}

		public void AddJunk(IList<Instruction> instrs) {
			if (Method.Module.IsClr40 || !JunkCode)
				return;

			switch (Random.NextInt32(6)) {
				case 0:
					instrs.Add(Instruction.Create(OpCodes.Pop));
					break;
				case 1:
					instrs.Add(Instruction.Create(OpCodes.Dup));
					break;
				case 2:
					instrs.Add(Instruction.Create(OpCodes.Throw));
					break;
				case 3:
					instrs.Add(Instruction.Create(OpCodes.Ldarg, new Parameter(0xff)));
					break;
				case 4:
					instrs.Add(Instruction.Create(OpCodes.Ldloc, new Local(null, null, 0xff)));
					break;
				case 5:
					instrs.Add(Instruction.Create(OpCodes.Ldtoken, Method));
					break;
			}
		}
	}
}
