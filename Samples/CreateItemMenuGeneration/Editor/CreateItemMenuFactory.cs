using Codice.ThemeImages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zenvin.ScriptGeneration.ItemMenu {
	public class CreateItemMenuFactory : DirectedScriptFactory, IScriptFactoryExtension {

		private static readonly Type BaseType = typeof (ScriptableObject);

		public int OrderInMenu { get; set; }
		public string GlobalPrefix { get; set; } = "";
		public string GlobalSuffix { get; set; } = "";

		public override bool AllowPlaymodeChanges => false;


		protected override void Setup () {
			GenerateScript ();
		}

		private void GenerateScript () {
			// validate output path
			bool validPath = ValidateOrCreateOutputPath (out string fullPath, out FileInfo file);
			if (!validPath) {
				LogError ($"Create Item Menu was not generated, because the output file path ('{fullPath}') was invalid.");
				return;
			}
			var types = TypeCache.GetTypesWithAttribute<CreateAssetInstanceMenuAttribute> ()
										.Where (IsTypeValid)
										.Select (t => new ValueTuple<Type, CreateAssetInstanceMenuAttribute> (t, t.GetCustomAttribute<CreateAssetInstanceMenuAttribute> (true))).ToList ();

			if (types.Count > 0) {
				GenerateFile (types, file);
			}
		}

		private void GenerateFile (List<ValueTuple<Type, CreateAssetInstanceMenuAttribute>> types, FileInfo outputFile) {
			var sb = new StringBuilder ();
			var used = new HashSet<string> ();
			var counter = 0;

			ScriptFactoryUtility.AppendAutoGeneratedWarning (sb);
			AppendHeader (sb);
			foreach (var type in types) {
				if (type.Item1 != null && type.Item2 != null) {
					GenerateCreateItemMethod (sb, type.Item1, type.Item2.Prefix, type.Item2.Suffix, used, ref counter);
					sb.AppendLine ();
				}
			}
			GenerateCreateAssetMethod (sb);
			AppendFooter (sb);

			using (var file = File.CreateText (outputFile.FullName)) {
				file.Write (sb.ToString ());
				file.Flush ();
			}
		}

		private void AppendHeader (StringBuilder sb) {
			sb.AppendLine ("#if UNITY_EDITOR");
			sb.AppendLine ();
			sb.AppendLine ("namespace Zenvin.ScriptGeneration.ItemMenu._AutoGenerated");
			sb.AppendLine ("{");
			sb.AppendLine ("\tinternal class __CreateItemMenuMethods");
			sb.AppendLine ("\t{");
		}

		private void AppendFooter (StringBuilder sb) {
			sb.AppendLine ("\t}");
			sb.AppendLine ("}");
			sb.AppendLine ("#endif");
			sb.AppendLine ();
		}

		private void GenerateCreateItemMethod (StringBuilder sb, Type itemType, string prefix, string suffix, HashSet<string> used, ref int counter) {
			if (itemType == null || !TryGetFirstUnused (used, out string typeName, itemType.Name, itemType.FullName)) {
				return;
			}

			typeName = $"{GlobalPrefix}{prefix}{typeName}{suffix}{GlobalSuffix}";
			sb.AppendLine ($"\t\t// Menu item for '{itemType.AssemblyQualifiedName}'");
			sb.AppendLine ($"\t\t[{typeof (MenuItem).FullName}(\"Assets/Create/{typeName}\", priority = {OrderInMenu})]");
			sb.AppendLine ($"\t\tprivate static void _Generate{counter}()");
			sb.AppendLine ("\t\t{");
			sb.AppendLine ($"\t\t\tvar item = {typeof (ScriptableObject).FullName}.CreateInstance(typeof({itemType.FullName}));");
			sb.AppendLine ($"\t\t\t_CreateAsset(item);");
			sb.AppendLine ("\t\t}");
		}

		private void GenerateCreateAssetMethod (StringBuilder sb) {
			sb.AppendLine ("\t\t// Utility method for creating asset instances");
			sb.AppendLine ($"\t\tprivate static void _CreateAsset({typeof (ScriptableObject).FullName} instance)");
			sb.AppendLine ("\t\t{");
			sb.AppendLine ($"\t\t\tvar path = {typeof (AssetDatabase).FullName}.{nameof (AssetDatabase.GetAssetPath)}({typeof (Selection).FullName}.{nameof (Selection.activeObject)});");
			sb.AppendLine ($"\t\t\tvar assetPath = path + \"/New Asset.asset\";");
			sb.AppendLine ($"\t\t\t{typeof (ProjectWindowUtil).FullName}.{nameof (ProjectWindowUtil.CreateAsset)}(instance, assetPath);");
			sb.AppendLine ($"\t\t\t{typeof (AssetDatabase).FullName}.{nameof (AssetDatabase.Refresh)}();");
			sb.AppendLine ($"\t\t\t{typeof (AssetDatabase).FullName}.{nameof (AssetDatabase.SaveAssets)}();");
			sb.AppendLine ("\t\t}");
		}

		private static bool TryGetFirstUnused (HashSet<string> used, out string result, params string[] values) {
			foreach (var value in values) {
				if (used.Add (value)) {
					result = value;
					return true;
				}
			}
			result = null;
			return false;
		}

		private static bool IsTypeValid (Type t) {
			return !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsSubclassOf (BaseType) && t.IsPublic;
		}


		GUIContent[] IScriptFactoryExtension.GetFactoryButtonLabels () {
			return new GUIContent[] {
				new GUIContent("Generate")
			};
		}

		bool IScriptFactoryExtension.IsFactoryButtonInteractable (int index) {
			return !Application.isPlaying;
		}

		void IScriptFactoryExtension.OnFactoryButtonClick (int index) {
			switch (index) {
				case 0:
					GenerateScript ();
					break;
			}
		}
	}
}