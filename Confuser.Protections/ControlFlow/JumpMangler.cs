using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.ControlFlow {
	internal class JumpMangler : ManglerBase {
		LinkedList<Instruction[]> SpiltFragments(InstrBlock block, CFContext ctx) {
			var fragments = new LinkedList<Instruction[]>();
			var currentFragment = new List<Instruction>();

			int skipCount = -1;
			for (int i = 0; i < block.Instructions.Count; i++) {
				if (skipCount != -1) {
					if (skipCount > 0) {
						currentFragment.Add(block.Instructions[i]);
						skipCount--;
						continue;
					}
					fragments.AddLast(currentFragment.ToArray());
					currentFragment.Clear();

					skipCount = -1;
				}

				if (block.Instructions[i].OpCode.OpCodeType == OpCodeType.Prefix) {
					skipCount = 1;
				}
				else if (HasInstructionSeq(block.Instructions, i,Code.Dup, Code.Ldvirtftn, Code.Newobj)) {
					skipCount = 2;
				}
				else if (HasInstructionSeq(block.Instructions, i,Code.Ldc_I4, Code.Newarr, Code.Dup, Code.Ldtoken, Code.Call)) { // Array initializer
					skipCount = 4;
				}
				else if (HasInstructionSeq(block.Instructions, i,Code.Ldftn, Code.Newobj)) { // Create delegate to function
					skipCount = 1;
				}
				currentFragment.Add(block.Instructions[i]);

				if (skipCount == -1 && ctx.Intensity > ctx.Random.NextDouble()) {
					fragments.AddLast(currentFragment.ToArray());
					currentFragment.Clear();
				}
			}

			if (currentFragment.Count > 0)
				fragments.AddLast(currentFragment.ToArray());

			return fragments;
		}

		private static bool HasInstructionSeq(List<Instruction> instructions, int offset, params Code[] codes) {
			if (offset + codes.Length > instructions.Count) return false;
			return !codes.Where((code, i) => instructions[i + offset].OpCode.Code != code).Any();
		}


		private static int CalculatePushes(List<Instruction> instructions) {
			int pushes = 0;

			for(int i = 0; i < instructions.Count; i++) {
				instructions[i].CalculateStackUsage(out int push, out int pop);
				if(i == 0) {
					pushes += push;
					continue;
				}
				pushes += push - pop;
			}
		
			return pushes;
		}

		public override void Mangle(CilBody body, ScopeBlock root, CFContext ctx) {
			body.MaxStack += 2;
			foreach (InstrBlock block in GetAllBlocks(root)) {
				LinkedList<Instruction[]> fragments = SpiltFragments(block, ctx);
				if (fragments.Count < 4) continue;

				LinkedListNode<Instruction[]> current = fragments.First;
				while (current.Next != null) {
					var newFragment = new List<Instruction>(current.Value);
					int pushes = CalculatePushes(newFragment);
					ctx.AddJump2(newFragment, current.Next.Value[0]);
					ctx.AddJunk(newFragment);
					current.Value = newFragment.ToArray();
					current = current.Next;
				}
				Instruction[] first = fragments.First.Value;
				fragments.RemoveFirst();
				Instruction[] last = fragments.Last.Value;
				fragments.RemoveLast();

				List<Instruction[]> newFragments = fragments.ToList();
				ctx.Random.Shuffle(newFragments);

				block.Instructions = first
					.Concat(newFragments.SelectMany(fragment => fragment))
					.Concat(last).ToList();
			}
			
		}
	}
}
