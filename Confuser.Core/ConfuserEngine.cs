using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core.Project;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Win32;
using InformationalAttribute = System.Reflection.AssemblyInformationalVersionAttribute;
using ProductAttribute = System.Reflection.AssemblyProductAttribute;
using CopyrightAttribute = System.Reflection.AssemblyCopyrightAttribute;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Confuser.Core {
	/// <summary>
	///     The processing engine of ConfuserEx.
	/// </summary>
	public static class ConfuserEngine
	{
		public static readonly string Version;

		private static readonly string Copyright;

		static ConfuserEngine()
		{
			var assembly = typeof(ConfuserEngine).Assembly;
			var nameAttr = (ProductAttribute)assembly.GetCustomAttributes(typeof(ProductAttribute), inherit: false)[0];
            var verAttr = (InformationalAttribute)assembly.GetCustomAttributes(typeof(InformationalAttribute), inherit: false)[0];
			var cpAttr = (CopyrightAttribute)assembly.GetCustomAttributes(typeof(CopyrightAttribute), inherit: false)[0];
		
			Version = $"{nameAttr.Product} {verAttr.InformationalVersion}";
			Copyright = cpAttr.Copyright;

			AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs e)
			{
				try
				{
					AssemblyName assemblyName = new AssemblyName(e.Name);
					Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
					foreach (Assembly assembly2 in assemblies)
					{
						if (assembly2.GetName().Name == assemblyName.Name)
						{
							return assembly2;
						}
					}
					return null;
				}
				catch
				{
					return null;
				}
			};
		}

		public static Task Run(ConfuserParameters parameters, CancellationToken? token = null)
		{
			if (parameters.Project == null)
			{
				throw new ArgumentNullException("parameters");
			}
			if (!token.HasValue)
			{
				token = new CancellationTokenSource().Token;
			}
			return Task.Factory.StartNew(delegate
			{
				RunInternal(parameters, token.Value);
			}, token.Value);
		}

		private static void RunInternal(ConfuserParameters parameters, CancellationToken token)
		{
			ConfuserContext context = new ConfuserContext();
			context.Logger = parameters.GetLogger();
			context.Project = parameters.Project.Clone();
			context.PackerInitiated = parameters.PackerInitiated;
			context.token = token;
			PrintInfo(context);
			bool ok = false;
			try
			{
				context.Project.Rules.Insert(0, new Rule());
				ConfuserAssemblyResolver asmResolver = new ConfuserAssemblyResolver
				{
					EnableTypeDefCache = true
				};
				asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
				context.InternalResolver = asmResolver;
				context.BaseDirectory = Path.Combine(Environment.CurrentDirectory, context.Project.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
				context.OutputDirectory = Path.Combine(context.Project.BaseDirectory, context.Project.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
				foreach (string probePath in context.Project.ProbePaths)
				{
					asmResolver.PostSearchPaths.Insert(0, Path.Combine(context.BaseDirectory, probePath));
				}
				context.CheckCancellation();
				Marker marker = parameters.GetMarker();
				context.Logger.Debug("Discovering plugins...");
				parameters.GetPluginDiscovery().GetPlugins(context, out var prots, out var packers, out var components);
				context.Logger.InfoFormat("Discovered {0} protections, {1} packers.", prots.Count, packers.Count);
				context.CheckCancellation();
				context.Logger.Debug("Resolving component dependency...");
				try
				{
					DependencyResolver resolver = new DependencyResolver(prots);
					prots = resolver.SortDependency();
				}
				catch (CircularDependencyException ex7)
				{
					context.Logger.ErrorException("", ex7);
					throw new ConfuserException(ex7);
				}
				components.Insert(0, new CoreComponent(context, marker));
				foreach (Protection prot in prots)
				{
					components.Add(prot);
				}
				foreach (Packer packer in packers)
				{
					components.Add(packer);
				}
				context.CheckCancellation();
				context.Logger.Info("Loading input modules...");
				marker.Initalize(prots, packers);
				MarkerResult markings = marker.MarkProject(context.Project, context);
				context.Modules = new ModuleSorter(markings.Modules).Sort().ToList().AsReadOnly();
				foreach (ModuleDefMD module in context.Modules)
				{
					module.EnableTypeDefFindCache = false;
				}
				context.OutputModules = Enumerable.Repeat<byte[]>(null, context.Modules.Count).ToArray();
				context.OutputSymbols = Enumerable.Repeat<byte[]>(null, context.Modules.Count).ToArray();
				context.OutputPaths = Enumerable.Repeat<string>(null, context.Modules.Count).ToArray();
				context.Packer = markings.Packer;
				context.ExternalModules = markings.ExternalModules;
				context.CheckCancellation();
				context.Logger.Info("Initializing...");
				foreach (ConfuserComponent comp2 in components)
				{
					try
					{
						comp2.Initialize(context);
					}
					catch (Exception ex6)
					{
						context.Logger.ErrorException("Error occured during initialization of '" + comp2.Name + "'.", ex6);
						throw new ConfuserException(ex6);
					}
					context.CheckCancellation();
				}
				context.CheckCancellation();
				context.Logger.Debug("Building pipeline...");
				ProtectionPipeline pipeline = (context.Pipeline = new ProtectionPipeline());
				foreach (ConfuserComponent comp in components)
				{
					comp.PopulatePipeline(pipeline);
				}
				context.CheckCancellation();
				RunPipeline(pipeline, context);
				ok = true;
			}
			catch (AssemblyResolveException ex5)
			{
				context.Logger.ErrorException("Failed to resolve an assembly, check if all dependencies are present in the correct version.", ex5);
				PrintEnvironmentInfo(context);
			}
			catch (TypeResolveException ex4)
			{
				context.Logger.ErrorException("Failed to resolve a type, check if all dependencies are present in the correct version.", ex4);
				PrintEnvironmentInfo(context);
			}
			catch (MemberRefResolveException ex3)
			{
				context.Logger.ErrorException("Failed to resolve a member, check if all dependencies are present in the correct version.", ex3);
				PrintEnvironmentInfo(context);
			}
			catch (IOException ex2)
			{
				context.Logger.ErrorException("An IO error occurred, check if all input/output locations are readable/writable.", ex2);
			}
			catch (OperationCanceledException)
			{
				context.Logger.Error("Operation cancelled.");
			}
			catch (ConfuserException)
			{
			}
			catch (Exception ex)
			{
				context.Logger.ErrorException("Unknown error occurred.", ex);
			}
			finally
			{
				if (context.Resolver != null)
				{
					context.InternalResolver.Clear();
				}
				context.Logger.Finish(ok);
			}
		}

		private static void RunPipeline(ProtectionPipeline pipeline, ConfuserContext context)
		{
			Func<IList<IDnlibDef>> getAllDefs = () => context.Modules.SelectMany((ModuleDefMD module) => module.FindDefinitions()).ToList();
			Func<ModuleDef, IList<IDnlibDef>> getModuleDefs = (ModuleDef module) => module.FindDefinitions().ToList();
			context.CurrentModuleIndex = -1;
			pipeline.ExecuteStage(PipelineStage.Inspection, Inspection, () => getAllDefs(), context);
			ModuleWriterOptionsBase[] options = new ModuleWriterOptionsBase[context.Modules.Count];
			for (int i = 0; i < context.Modules.Count; i++)
			{
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = null;
				pipeline.ExecuteStage(PipelineStage.BeginModule, BeginModule, () => getModuleDefs(context.CurrentModule), context);
				pipeline.ExecuteStage(PipelineStage.ProcessModule, ProcessModule, () => getModuleDefs(context.CurrentModule), context);
				pipeline.ExecuteStage(PipelineStage.OptimizeMethods, OptimizeMethods, () => getModuleDefs(context.CurrentModule), context);
				pipeline.ExecuteStage(PipelineStage.EndModule, EndModule, () => getModuleDefs(context.CurrentModule), context);
				options[i] = context.CurrentModuleWriterOptions;
			}
			for (int j = 0; j < context.Modules.Count; j++)
			{
				context.CurrentModuleIndex = j;
				context.CurrentModuleWriterOptions = options[j];
				pipeline.ExecuteStage(PipelineStage.WriteModule, WriteModule, () => getModuleDefs(context.CurrentModule), context);
				context.OutputModules[j] = context.CurrentModuleOutput;
				context.OutputSymbols[j] = context.CurrentModuleSymbol;
				context.CurrentModuleWriterOptions = null;
				context.CurrentModuleOutput = null;
				context.CurrentModuleSymbol = null;
			}
			context.CurrentModuleIndex = -1;
			pipeline.ExecuteStage(PipelineStage.Debug, Debug, () => getAllDefs(), context);
			pipeline.ExecuteStage(PipelineStage.Pack, Pack, () => getAllDefs(), context);
			pipeline.ExecuteStage(PipelineStage.SaveModules, SaveModules, () => getAllDefs(), context);
			if (!context.PackerInitiated)
			{
				context.Logger.Info("Done.");
			}
		}

		private static void Inspection(ConfuserContext context)
		{
			context.Logger.Info("Resolving dependencies...");
			foreach (Tuple<dnlib.DotNet.AssemblyRef, ModuleDefMD> dependency in context.Modules.SelectMany((ModuleDefMD module) => from asmRef in module.GetAssemblyRefs()
																																   select Tuple.Create(asmRef, module)))
			{
				try
				{
					context.Resolver.ResolveThrow(dependency.Item1, dependency.Item2);
				}
				catch (AssemblyResolveException ex)
				{
					context.Logger.ErrorException(string.Concat("Failed to resolve dependency of '", dependency.Item2.Name, "'."), ex);
					throw new ConfuserException(ex);
				}
			}
			context.Logger.Debug("Checking Strong Name...");
			foreach (ModuleDefMD module3 in context.Modules)
			{
				CheckStrongName(context, module3);
			}
			IMarkerService marker = context.Registry.GetService<IMarkerService>();
			context.Logger.Debug("Creating global .cctors...");
			foreach (ModuleDefMD module2 in context.Modules)
			{
				TypeDef modType = module2.GlobalType;
				if (modType == null)
				{
					modType = new TypeDefUser("", "<Module>", null);
					modType.Attributes = dnlib.DotNet.TypeAttributes.NotPublic;
					module2.Types.Add(modType);
					marker.Mark(modType, null);
				}
				MethodDef cctor = modType.FindOrCreateStaticConstructor();
				if (!marker.IsMarked(cctor))
				{
					marker.Mark(cctor, null);
				}
			}
		}

		private static void CheckStrongName(ConfuserContext context, ModuleDef module)
		{
			StrongNameKey snKey = context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
			byte[] snPubKeyBytes = context.Annotations.Get<StrongNamePublicKey>(module, Marker.SNPubKey)?.CreatePublicKey();
			bool snDelaySign = context.Annotations.Get(module, Marker.SNDelaySig, defValue: false);
			if (snPubKeyBytes == null && snKey != null)
			{
				snPubKeyBytes = snKey.PublicKey;
			}
			bool moduleIsSignedOrDelayedSigned = module.IsStrongNameSigned || !module.Assembly.PublicKey.IsNullOrEmpty;
			bool isKeyProvided = snKey != null || (snDelaySign && snPubKeyBytes != null);
			if (!isKeyProvided && moduleIsSignedOrDelayedSigned)
			{
				context.Logger.WarnFormat("[{0}] SN Key or SN public Key is not provided for a signed module, the output may not be working.", module.Name);
			}
			else if (isKeyProvided && !moduleIsSignedOrDelayedSigned)
			{
				context.Logger.WarnFormat("[{0}] SN Key or SN public Key is provided for an unsigned module, the output may not be working.", module.Name);
			}
			else if (snPubKeyBytes != null && moduleIsSignedOrDelayedSigned && !module.Assembly.PublicKey.Data.SequenceEqual(snPubKeyBytes))
			{
				context.Logger.WarnFormat("[{0}] Provided SN public Key and signed module's public key do not match, the output may not be working.", module.Name);
			}
		}

		private static void CopyPEHeaders(PEHeadersOptions writerOptions, ModuleDefMD module)
		{
			var image = module.Metadata.PEImage;
			writerOptions.MajorImageVersion = image.ImageNTHeaders.OptionalHeader.MajorImageVersion;
			writerOptions.MajorLinkerVersion = image.ImageNTHeaders.OptionalHeader.MajorLinkerVersion;
			writerOptions.MajorOperatingSystemVersion = image.ImageNTHeaders.OptionalHeader.MajorOperatingSystemVersion;
			writerOptions.MajorSubsystemVersion = image.ImageNTHeaders.OptionalHeader.MajorSubsystemVersion;
			writerOptions.MinorImageVersion = image.ImageNTHeaders.OptionalHeader.MinorImageVersion;
			writerOptions.MinorLinkerVersion = image.ImageNTHeaders.OptionalHeader.MinorLinkerVersion;
			writerOptions.MinorOperatingSystemVersion = image.ImageNTHeaders.OptionalHeader.MinorOperatingSystemVersion;
			writerOptions.MinorSubsystemVersion = image.ImageNTHeaders.OptionalHeader.MinorSubsystemVersion;
		}

		private static void BeginModule(ConfuserContext context)
		{
			context.Logger.InfoFormat("Processing module '{0}'...", context.CurrentModule.Name);
			context.CurrentModuleWriterOptions = new ModuleWriterOptions(context.CurrentModule);
			CopyPEHeaders(context.CurrentModuleWriterOptions.PEHeadersOptions, context.CurrentModule);
			if (!context.CurrentModule.IsILOnly || context.CurrentModule.VTableFixups != null)
			{
				context.RequestNative(optimizeImageSize: true);
			}
			StrongNameKey snKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNKey);
			StrongNamePublicKey snPubKey = context.Annotations.Get<StrongNamePublicKey>(context.CurrentModule, Marker.SNPubKey);
			StrongNameKey snSigKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNSigKey);
			StrongNamePublicKey snSigPubKey = context.Annotations.Get<StrongNamePublicKey>(context.CurrentModule, Marker.SNSigPubKey);
			bool snDelaySig = context.Annotations.Get(context.CurrentModule, Marker.SNDelaySig, defValue: false);
			context.CurrentModuleWriterOptions.DelaySign = snDelaySig;
			if (snKey != null && snPubKey != null && snSigKey != null && snSigPubKey != null)
			{
				context.CurrentModuleWriterOptions.InitializeEnhancedStrongNameSigning(context.CurrentModule, snSigKey, snSigPubKey, snKey, snPubKey);
			}
			else if (snSigPubKey != null && snSigKey != null)
			{
				context.CurrentModuleWriterOptions.InitializeEnhancedStrongNameSigning(context.CurrentModule, snSigKey, snSigPubKey);
			}
			else
			{
				context.CurrentModuleWriterOptions.InitializeStrongNameSigning(context.CurrentModule, snKey);
			}
			if (snDelaySig)
			{
				context.CurrentModuleWriterOptions.StrongNamePublicKey = snPubKey;
				context.CurrentModuleWriterOptions.StrongNameKey = null;
			}
			foreach (TypeDef type in context.CurrentModule.GetTypes())
			{
				foreach (MethodDef method in type.Methods)
				{
					if (method.Body != null)
					{
						method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
					}
				}
			}
		}

		private static void ProcessModule(ConfuserContext context)
		{
			context.CurrentModuleWriterOptions.WriterEvent += delegate
			{
				context.CheckCancellation();
			};
		}

		private static void OptimizeMethods(ConfuserContext context)
		{
			foreach (TypeDef type in context.CurrentModule.GetTypes())
			{
				foreach (MethodDef method in type.Methods)
				{
					if (method.Body != null)
					{
						method.Body.Instructions.OptimizeMacros();
					}
				}
			}
		}

		private static void EndModule(ConfuserContext context)
		{
			string output = context.Modules[context.CurrentModuleIndex].Location;
			if (output != null)
			{
				if (!Path.IsPathRooted(output))
				{
					output = Path.Combine(context.BaseDirectory, output);
				}
				string relativeOutput = Utils.GetRelativePath(output, context.BaseDirectory);
				if (relativeOutput == null)
				{
					context.Logger.WarnFormat("Input file is not inside the base directory. Relative path can't be created. Placing file into output root." + Environment.NewLine + "Responsible file is: {0}", output);
					output = Path.GetFileName(output);
				}
				else
				{
					output = relativeOutput;
				}
			}
			else
			{
				output = context.CurrentModule.Name;
			}
			context.OutputPaths[context.CurrentModuleIndex] = output;
		}

		private static void WriteModule(ConfuserContext context)
		{
			context.Logger.InfoFormat("Writing module '{0}'...", context.CurrentModule.Name);
			MemoryStream pdb = null;
			MemoryStream output = new MemoryStream();
			context.CurrentModuleWriterOptions.Logger = DummyLogger.NoThrowInstance;
			if (context.CurrentModule.PdbState != null)
			{
				pdb = new MemoryStream();
				context.CurrentModuleWriterOptions.WritePdb = true;
				context.CurrentModuleWriterOptions.PdbFileName = Path.ChangeExtension(Path.GetFileName(context.OutputPaths[context.CurrentModuleIndex]), "pdb");
				context.CurrentModuleWriterOptions.PdbStream = pdb;
			}
			if (context.CurrentModuleWriterOptions is ModuleWriterOptions)
			{
				context.CurrentModule.Write(output, (ModuleWriterOptions)context.CurrentModuleWriterOptions);
			}
			else
			{
				context.CurrentModule.NativeWrite(output, (NativeModuleWriterOptions)context.CurrentModuleWriterOptions);
			}
			context.CurrentModuleOutput = output.ToArray();
			if (context.CurrentModule.PdbState != null)
			{
				context.CurrentModuleSymbol = pdb.ToArray();
			}
		}

		private static void Debug(ConfuserContext context)
		{
			context.Logger.Info("Finalizing...");
			for (int i = 0; i < context.OutputModules.Count; i++)
			{
				if (context.OutputSymbols[i] != null)
				{
					string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
					string dir = Path.GetDirectoryName(path);
					if (!Directory.Exists(dir))
					{
						Directory.CreateDirectory(dir);
					}
					File.WriteAllBytes(Path.ChangeExtension(path, "pdb"), context.OutputSymbols[i]);
				}
			}
		}

		private static void Pack(ConfuserContext context)
		{
			if (context.Packer != null)
			{
				context.Logger.Info("Packing...");
				context.Packer.Pack(context, new ProtectionParameters(context.Packer, context.Modules.OfType<IDnlibDef>().ToList()));
			}
		}

		private static void SaveModules(ConfuserContext context)
		{
			context.InternalResolver.Clear();
			for (int i = 0; i < context.OutputModules.Count; i++)
			{
				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}
				context.Logger.DebugFormat("Saving to '{0}'...", path);
				File.WriteAllBytes(path, context.OutputModules[i]);
			}
		}

		private static void PrintInfo(ConfuserContext context)
		{
			if (context.PackerInitiated)
			{
				context.Logger.Info("Protecting packer stub...");
				return;
			}
			context.Logger.InfoFormat("{0} {1}", Version, Copyright);
			Type mono = Type.GetType("Mono.Runtime");
			context.Logger.InfoFormat("Running on {0}, {1}, {2} bits", Environment.OSVersion, (mono == null) ? (".NET Framework v" + Environment.Version) : mono.GetMethod("GetDisplayName", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null), IntPtr.Size * 8);
		}

		private static IEnumerable<string> GetFrameworkVersions()
		{
			using (RegistryKey registryKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\"))
			{
				string[] subKeyNames = registryKey.GetSubKeyNames();
				foreach (string versionKeyName in subKeyNames)
				{
					if (!versionKeyName.StartsWith("v"))
					{
						continue;
					}
					RegistryKey versionKey = registryKey.OpenSubKey(versionKeyName);
					string name2 = (string)versionKey.GetValue("Version", "");
					string sp = versionKey.GetValue("SP", "").ToString();
					string install2 = versionKey.GetValue("Install", "").ToString();
					if (install2 == "" || (sp != "" && install2 == "1"))
					{
						yield return versionKeyName + "  " + name2;
					}
					if (name2 != "")
					{
						continue;
					}
					string[] subKeyNames2 = versionKey.GetSubKeyNames();
					foreach (string subKeyName in subKeyNames2)
					{
						RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
						name2 = (string)subKey.GetValue("Version", "");
						if (name2 != "")
						{
							subKey.GetValue("SP", "").ToString();
						}
						install2 = subKey.GetValue("Install", "").ToString();
						if (install2 == "")
						{
							yield return versionKeyName + "  " + name2;
						}
						else if (install2 == "1")
						{
							yield return "  " + subKeyName + "  " + name2;
						}
					}
				}
			}
			using RegistryKey ndpKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\\");
			if (ndpKey.GetValue("Release") != null)
			{
				yield return "v4.5 " + (int)ndpKey.GetValue("Release");
				yield break;
			}
		}

		private static void PrintEnvironmentInfo(ConfuserContext context)
		{
			if (context.PackerInitiated)
			{
				return;
			}
			context.Logger.Error("---BEGIN DEBUG INFO---");
			context.Logger.Error("Installed Framework Versions:");
			foreach (string ver in GetFrameworkVersions())
			{
				context.Logger.ErrorFormat("    {0}", ver.Trim());
			}
			context.Logger.Error("");
			if (context.Resolver != null)
			{
				context.Logger.Error("Cached assemblies:");
				foreach (AssemblyDef asm in context.InternalResolver.GetCachedAssemblies())
				{
					if (string.IsNullOrEmpty(asm.ManifestModule.Location))
					{
						context.Logger.ErrorFormat("    {0}", asm.FullName);
					}
					else
					{
						context.Logger.ErrorFormat("    {0} ({1})", asm.FullName, asm.ManifestModule.Location);
					}
					foreach (dnlib.DotNet.AssemblyRef reference in asm.Modules.OfType<ModuleDefMD>().SelectMany((ModuleDefMD m) => m.GetAssemblyRefs()))
					{
						context.Logger.ErrorFormat("        {0}", reference.FullName);
					}
				}
			}
			context.Logger.Error("---END DEBUG INFO---");
		}
	}
}
