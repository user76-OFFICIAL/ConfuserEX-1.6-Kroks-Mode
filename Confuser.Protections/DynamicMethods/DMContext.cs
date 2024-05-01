using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.DynamicMethods {
	internal class DMContext {

		public ModuleDef CurrentModule {
			get;
			set;
		}

		public TypeSig OpCodeTypeSig {
			get;
			set;
		}

		public TypeSig DynamicMethodTypeSig {
			get;
			set;
		}

		public TypeSig ILGeneratorTypeSig {
			get;
			set;
		}

		public TypeSig LocalBuilderTypeSig {
			get;
			set;
		}

		public TypeSig LabelTypeSig {
			get;
			set;
		}

		public IMethod GetILGenerator {
			get;
			set;
		}

		public IMethod DeclareLocal {
			get;
			set;
		}

		public IMethod DefineLabel {
			get;
			set;
		}

		public IMethod MarkLabel {
			get;
			set;
		}

		public IMethod EmitInlineNone {
			get;
			set;
		}

		public IMethod EmitInlineString {
			get;
			set;
		}

		public IMethod EmitInlineVar {
			get;
			set;
		}

		public IMethod EmitInlineBr {
			get;
			set;
		}

		public IMethod EmitInlineI {
			get;
			set;
		}

		public IMethod EmitInlineI8 {
			get;
			set;
		}


		public IMethod EmitInlineR {
			get;
			set;
		}
		public IMethod EmitInlineType {
			get;
			set;
		}

		public IMethod EmitInlineMethod {
			get;
			set;
		}

		public IMethod EmitInlineConstructor {
			get;
			set;
		}

		public IMethod EmitInlineField {
			get;
			set;
		}

		public IMethod Invoke {
			get;
			set;
		}


		public MethodDef CreateDynamicMethod {
			get;
			set;
		}

		public MethodDef TypeOf {
			get;
			set;
		}

		public MethodDef GetOpCode {
			get;
			set;
		}

		public MethodDef GetTypeMethod {
			get;
			set;
		}

		public MethodDef GetGenericType {
			get;
			set;
		}

		public MethodDef GetMethod {
			get;
			set;
		}

		public MethodDef GetGenericMethod {
			get;
			set;
		}

		public MethodDef GetConstructor {
			get;
			set;
		}

		public MethodDef GetField {
			get;
			set;
		}

		public MethodDef GetCached {
			get;
			set;
		}

		public MethodDef AddToCache {
			get;
			set;
		}
	}
}
