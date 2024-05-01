using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ConstantsMutation.Mutations {
	internal class SizeofMutation : ConstantMutation {

		private TypeRef _valueTypeRef;
		private TypeRef _intPtrTypeRef;

		public SizeofMutation(ModuleDef module) : base(module) {

		}

		protected override void Initialise(ModuleDef module) {
			_valueTypeRef = module.CorLibTypes.GetTypeRef("System", "ValueType");
			_intPtrTypeRef = module.CorLibTypes.GetTypeRef("System", "IntPtr");
		}

		public override void Mutate(IList<Instruction> instructions, int index) {
			var instruction = instructions[index];
			int value = instruction.GetLdcI4Value();

			bool odd = value % 2 != 0;

			if (odd)
				value++;


			instruction.OpCode = OpCodes.Sizeof;
			instruction.Operand = _intPtrTypeRef; 
			instructions.Add(OpCodes.Ldc_I4_4.ToInstruction());

			Instruction label64Bit = OpCodes.Nop.ToInstruction();
			Instruction labelExit = OpCodes.Nop.ToInstruction();

			instructions.Add(OpCodes.Ceq.ToInstruction());
			instructions.Add(OpCodes.Brfalse.ToInstruction(label64Bit));

			//Fallthrough 32-Bit handling
			instructions.Add(OpCodes.Sizeof.ToInstruction(_intPtrTypeRef));

			if (value % 4 == 0) {
				instructions.Add(Instruction.CreateLdcI4(value / 4));
			}
			else {
				instructions.Add(OpCodes.Ldc_I4_2.ToInstruction());
				instructions.Add(OpCodes.Div.ToInstruction());
				instructions.Add(Instruction.CreateLdcI4(value / 2));
			}

			instructions.Add(OpCodes.Mul.ToInstruction());
			instructions.Add(OpCodes.Br.ToInstruction(labelExit));


			instructions.Add(label64Bit);

			//64-Bit handling
			instructions.Add(OpCodes.Sizeof.ToInstruction(_intPtrTypeRef));
			if (value % 8 == 0) {
				instructions.Add(Instruction.CreateLdcI4(value / 8));
			}
			else {
				instructions.Add(OpCodes.Ldc_I4_4.ToInstruction());
				instructions.Add(OpCodes.Div.ToInstruction());
				instructions.Add(Instruction.CreateLdcI4(value / 2));
			}

			instructions.Add(OpCodes.Mul.ToInstruction());

			instructions.Add(labelExit);

			if(odd) {
				instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
				instructions.Add(OpCodes.Sub.ToInstruction());
			}
		}
	}
}
