//  This Source Code Form is subject to the terms of the Mozilla Public
//  License, v. 2.0. If a copy of the MPL was not distributed with this
//  file, You can obtain one at https://mozilla.org/MPL/2.0/. 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using File = System.IO.File;

/// <summary>
/// Will try to save time by automating some unity code manipulations.
/// </summary>
[InitializeOnLoad]
class MeowCodeHooks : Editor
{
	static MeowCodeHooks()
	{
		UnityEditor.Compilation.CompilationPipeline.compilationStarted
			+= CompilationPipelineOncompilationStarted;
	}

	private static string key = "meowcode";
	private static char[] singles = { '.', '{', '}', '+', '$', '(', ')', ';', ',', '[', ']', '<', '>' };
	private static HashSet<string> protection = new HashSet<string> { "public", "protected", "private" };

	struct Token
	{
		public string text;
		public int line;
	}


	struct DispoableClassData
	{
		public Type type;
		public List<FieldInfo> DisposableMembers;
	}


	static void SkipBlock(ref List<Token>.Enumerator Enum)
	{
		if (Enum.Current.text != "{") return;

		while (Enum.MoveNext())
		{
			if (Enum.Current.text == "}")
			{
				return;
			}
			if (Enum.Current.text == "{")
			{
				SkipBlock(ref Enum);
			}
		}
	}
	
	
	private static void ProcessFile(string fileName, Dictionary<string, DispoableClassData> knownDisposables)
	{
		string[] allLines =File.ReadAllLines(fileName);

		var original = new List<string>();

		bool bInMeowBlock = false;
		foreach (string s in allLines)
		{
			if (s == "#region " + key)
			{
				bInMeowBlock = true;
			}
			else if (s == "#endregion " + key)
			{
				bInMeowBlock = false;
			}
			else if (!bInMeowBlock)
			{
				original.Add(s);
			}
		}
		

		var Tokens = new List<Token>();
		var line = 0;
		foreach (string s in original)
		{
			int start = 0;
			int cur = 0;
			
			while (cur < s.Length && (s[start] == ' ' || s[start] == '\t'))
			{
				start++;
				cur++;
			}

			while (cur < s.Length-1)
			{
				while (cur < s.Length-1 && (s[start] == ' ' || s[start] == '\t'))
				{
					start++;
					cur++;
				}

				bool bComment = false;
				while (cur < s.Length-1)
				{
					if (s[cur] == '/' && s[cur + 1] == '/')
					{
						bComment = true;
						break;
					}

					var n = s[cur + 1];
					if (n == ' ' || n == '\t')
					{
						var nt = new Token();
						nt.line = line;
						nt.text = s.Substring(start, cur - start + 1);
						Tokens.Add(nt);
						cur++;
						start = cur;
						break;
					}

					if (singles.Contains(n))
					{
						var nt = new Token();
						nt.line = line;
						nt.text = s.Substring(start, cur - start + 1);
						Tokens.Add(nt);
						
						cur++;
						start = cur;
						
						nt = new Token();
						nt.line = line;
						nt.text = s.Substring(start, cur - start + 1);
						Tokens.Add(nt);

						cur++;
						start = cur;
					}
					else
					{
						cur++;
					}
				}

				if (bComment)
				{
					break;
				}
			}

			if (start < s.Length)
			{
				var nt = new Token();
				nt.line = line;
				nt.text = s.Substring(start, s.Length - start);
				Tokens.Add(nt);
			}
			
			line++;
		}

		string className = "";
		int lineOfDisposeEnd = -1;
		var Enum = Tokens.GetEnumerator();
		while (true)
		{
			if (Enum.Current.text == "using")
			{
				while (Enum.MoveNext())
				{
					if (Enum.Current.text == ";")
						break;
				}
				Enum.MoveNext();
			}
			else if (Enum.Current.text == "class" || Enum.Current.text == "struct")
			{
				Enum.MoveNext();

				if (!knownDisposables.ContainsKey(Enum.Current.text))
				{
					return;
				}

				className = Enum.Current.text;

				Enum.MoveNext();

				if (Enum.Current.text == "<")
				{
					while (Enum.Current.text != ">")
					{
						Enum.MoveNext();
					}

					Enum.MoveNext();
				}

				if (Enum.Current.text == ":")
				{
					do
					{
						Enum.MoveNext();

						if (Enum.Current.text == "IDisposable")
						{
							return;
						}

						Enum.MoveNext();
					} while (Enum.Current.text == ",");
				}

				if (Enum.Current.text == "{")
				{
					Enum.MoveNext();
					do
					{
						if (protection.Contains(Enum.Current.text))
						{
							Enum.MoveNext();
						}
						else if (Enum.Current.text == "void")
						{
							Enum.MoveNext();
							if (Enum.Current.text == "Dispose")
							{
								while (Enum.MoveNext())
								{
									if (Enum.Current.text == "{")
									{
										SkipBlock(ref Enum);
										lineOfDisposeEnd = Enum.Current.line;
										break;;
									}
								}
							}
						}
						else if (Enum.Current.text == "{")
						{
							SkipBlock(ref Enum);
							Enum.MoveNext();
						}
						else
						{
							Enum.MoveNext();
						}
					} while (Enum.Current.text != "}");
				}
			}
			else if (!Enum.MoveNext())
			{
				break;
			}
		}
		

		/*foreach (var T in Tokens)
		{
			Debug.Log($"{fileName} {T.text} {T.line}");
		}*/

		if (lineOfDisposeEnd > 0)
		{
			var generated = new List<string>();

			int i = 0;
			foreach (string s in original)
			{
				generated.Add(s);
				
				if (i == lineOfDisposeEnd)
				{
					generated.Add("#region " + key);
					foreach (var disp in knownDisposables[className].DisposableMembers)
					{
						generated.Add($"\tprivate bool _meowDisposed_{disp.Name} = false;");
					}
					generated.Add("\tprotected virtual void Dispose(bool disposing)");
					generated.Add("\t{");
					foreach (var disp in knownDisposables[className].DisposableMembers)
					{
						generated.Add($"\t\tif (!_meowDisposed_{disp.Name})");
						generated.Add("\t\t{");
						generated.Add($"\t\t\t{disp.Name}.Dispose();");
						generated.Add($"\t\t\t_meowDisposed_{disp.Name} = true;");
						generated.Add("\t\t}");
					}
					generated.Add("\t}");
					generated.Add("");
					generated.Add($"\t~{className}() => Dispose(false);");
					generated.Add("#endregion " + key);
				}
				i += 1;

				if (i == lineOfDisposeEnd)
				{
					generated.Add("#region " + key);
					generated.Add("\t\tDispose(true);");
					generated.Add("\t\t GC.SuppressFinalize(this);");
					generated.Add("#endregion " + key);
				}
			}

			if (generated.Count == allLines.Length)
			{
				bool bIsIdentical = true;
				for (int j = 0; i < generated.Count; i++)
				{
					if (generated[i] != allLines[i])
					{
						bIsIdentical = false;
						break;
					}
				}

				if (bIsIdentical)
				{
					return;
				}
			}
			
			Debug.Log(
				$"{fileName} / {lineOfDisposeEnd} Generated lines: {generated.Count} ({allLines.Length}/{original.Count})");
			File.WriteAllLines(fileName, generated);
		}
	}

	private static void CompilationPipelineOncompilationStarted(object obj)
	{
		if (MeowCodeMenu.IsCodeGenEnable())
		{
			// For every type deriving from IAutoDisposable, keep track of the disposables within
			var AutoDisposables = new Dictionary<string, DispoableClassData>();

			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (Type t in a.GetTypes())
				{
					if (typeof(IAutoDisposable).IsAssignableFrom(t) && !t.IsInterface)
					{
						var classData = new DispoableClassData();
						var foundDisposables = new List<FieldInfo>();

						foreach (var member in t.GetFields(
							         BindingFlags.NonPublic 
							         | BindingFlags.Public
							         | BindingFlags.Instance
							         | BindingFlags.DeclaredOnly))
						{
							if (typeof(IDisposable).IsAssignableFrom(member.FieldType))
							{
								foundDisposables.Add(member);
							}
						}

						classData.type = t;
						classData.DisposableMembers = foundDisposables;
						AutoDisposables.Add(t.Name, classData);
					}
				}
			}

			var Files = System.IO.Directory.EnumerateFiles(
				Application.dataPath,
				"*.cs",
				SearchOption.AllDirectories);
			foreach (string F in Files)
			{
				if (F.Contains("Editor"))
				{
					continue;
				}

				try
				{
					ProcessFile(F, AutoDisposables);
				}
				catch (Exception e)
				{
					Debug.LogError(e.ToString());
				}
			}
		}
	}
}