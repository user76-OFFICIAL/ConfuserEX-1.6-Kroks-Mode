using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class EncodePhase : ProtectionPhase {
		public EncodePhase(ConstantProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Constants encoding"; }
		}

		private void CompressBuffer(CEContext moduleCtx, ConfuserContext context) {
			byte[] encodedBuff = new byte[moduleCtx.EncodedBuffer.Count * 4];
			int buffIndex = 0;
			foreach (uint dat in moduleCtx.EncodedBuffer) {
				encodedBuff[buffIndex++] = (byte)((dat >> 0) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 8) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 16) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 24) & 0xff);
			}
			Debug.Assert(buffIndex == encodedBuff.Length);
			encodedBuff = context.Registry.GetService<ICompressionService>().Compress(encodedBuff);
			context.CheckCancellation();

			uint compressedLen = (uint)(encodedBuff.Length + 3) / 4;
			compressedLen = (compressedLen + 0xfu) & ~0xfu;
			uint[] compressedBuff = new uint[compressedLen];
			Buffer.BlockCopy(encodedBuff, 0, compressedBuff, 0, encodedBuff.Length);
			Debug.Assert(compressedLen % 0x10 == 0);

			// encrypt
			uint keySeed = moduleCtx.Random.NextUInt32();
			uint[] key = new uint[0x10];
			uint state = keySeed;
			for (int i = 0; i < 0x10; i++) {
				state ^= state >> 12;
				state ^= state << 25;
				state ^= state >> 27;
				key[i] = state;
			}

			var encryptedBuffer = new byte[compressedBuff.Length * 4];
			buffIndex = 0;
			while (buffIndex < compressedBuff.Length) {
				uint[] enc = moduleCtx.ModeHandler.Encrypt(compressedBuff, buffIndex, key);
				for (int j = 0; j < 0x10; j++)
					key[j] ^= compressedBuff[buffIndex + j];
				Buffer.BlockCopy(enc, 0, encryptedBuffer, buffIndex * 4, 0x40);
				buffIndex += 0x10;
			}
			Debug.Assert(buffIndex == compressedBuff.Length);

			moduleCtx.DataField.InitialValue = encryptedBuffer;
			moduleCtx.DataField.HasFieldRVA = true;
			moduleCtx.DataType.ClassLayout = new ClassLayoutUser(0, (uint)encryptedBuffer.Length);

			MutationHelper.InjectKeys(moduleCtx.InitMethod,
									  new[] { 0, 1 },
									  new[] { encryptedBuffer.Length / 4, (int)keySeed });
			MutationHelper.ReplacePlaceholder(moduleCtx.InitMethod, arg => {
				var repl = new List<Instruction>();
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));
				repl.Add(Instruction.Create(OpCodes.Call, moduleCtx.Module.Import(
					typeof(RuntimeHelpers).GetMethod("InitializeArray"))));
				return repl.ToArray();
			});
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var moduleCtx = context.Annotations.Get<CEContext>(context.CurrentModule, ConstantProtection.ContextKey);
			if (!parameters.Targets.Any() || moduleCtx == null)
				return;

			var ldc = new Dictionary<object, List<InstructionReference>>();

			// Extract constants
			ExtractConstants(context, parameters, ldc);

			// Encode constants
			moduleCtx.ReferenceRepl = new Dictionary<MethodDef, List<ReplaceableInstructionReference>>();
			moduleCtx.EncodedBuffer = new List<uint>();

			foreach (var entry in ldc.WithProgress(context.Logger)) {
				if (entry.Key is string) {
					EncodeString(moduleCtx, (string)entry.Key, entry.Value);
				}
				context.CheckCancellation();
			}
			ReferenceReplacer.ReplaceReference(context, moduleCtx, parameters);
			CompressBuffer(moduleCtx, context);
		}

		int EncodeByteArray(CEContext moduleCtx, byte[] buff) {
			int buffIndex = moduleCtx.EncodedBuffer.Count;
			moduleCtx.EncodedBuffer.Add((uint)buff.Length);

			// byte[] -> uint[]
			int integral = buff.Length / 4, remainder = buff.Length % 4;
			for (int i = 0; i < integral; i++) {
				var data = (uint)(buff[i * 4] | (buff[i * 4 + 1] << 8) | (buff[i * 4 + 2] << 16) | (buff[i * 4 + 3] << 24));
				moduleCtx.EncodedBuffer.Add(data);
			}
			if (remainder > 0) {
				int baseIndex = integral * 4;
				uint r = 0;
				for (int i = 0; i < remainder; i++)
					r |= (uint)(buff[baseIndex + i] << (i * 8));
				moduleCtx.EncodedBuffer.Add(r);
			}
			return buffIndex;
		}

		void EncodeString(CEContext moduleCtx, string value, List<InstructionReference> references) {
			int buffIndex = EncodeByteArray(moduleCtx, Encoding.UTF8.GetBytes(value));
			UpdateReference(moduleCtx, references, buffIndex, desc => desc.StringID);
		}

		void UpdateReference(CEContext moduleCtx, List<InstructionReference>  references, int buffIndex, Func<DecoderDesc, byte> typeID) {

			foreach (var instr in references) {
					int i = moduleCtx.Random.NextInt32(0, moduleCtx.Decoders.Count - 1);
					DecoderInfo decoderInfo = moduleCtx.Decoders[i];
					DecoderDesc desc = decoderInfo.DecoderDesc;

					uint id = (uint)buffIndex | (uint)(typeID(desc) << 30);
					id = moduleCtx.ModeHandler.Encode(desc.Data, moduleCtx, id);

					uint key = moduleCtx.Random.NextUInt32();
					id ^= key;

					moduleCtx.ReferenceRepl.AddListEntry(instr.Method, new ReplaceableInstructionReference {
						Target = instr.Instruction,
						Id = id,
						Key = key,
						Decoder = decoderInfo.Method
					});
			}
		}


		void ExtractConstants(ConfuserContext context, ProtectionParameters parameters, Dictionary<object, List<InstructionReference>> ldc) {
			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(context.Logger)) {
				if (!method.HasBody)
					continue;

				foreach (Instruction instr in method.Body.Instructions) {
					if (instr.OpCode != OpCodes.Ldstr) {
						continue;
					}
					string operand = (string)instr.Operand;
					if (string.IsNullOrEmpty(operand)) {
						continue;
					}
					ldc.AddListEntry(operand, new InstructionReference {
						Method = method,
						Instruction = instr,
					});
				}

				context.CheckCancellation();
			}
		}

	}
}
