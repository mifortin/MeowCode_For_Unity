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


	private static void SkipWhiteSpace(string Source, ref int Start, ref int Cur)
	{
		while (Cur < Source.Length && (Source[Start] == ' ' || Source[Start] == '\t'))
		{
			Start++;
			Cur++;
		}
	}


	private static void SkipBlock(ref List<Token>.Enumerator Enum)
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
			
			SkipWhiteSpace(s, ref start, ref cur);

			while (cur < s.Length-1)
			{
				SkipWhiteSpace(s, ref start, ref cur);

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
						// Close current token that we've been accumulating.
						var nt = new Token();
						if (cur != start)
						{
							nt.line = line;
							nt.text = s.Substring(start, cur - start + 1);
							Tokens.Add(nt);
						}

						cur++;
						start = cur;
						
						// Add the single token afterwards.
						nt = new Token();
						nt.line = line;
						nt.text = s.Substring(start, cur - start + 1);
						Tokens.Add(nt);

						cur++;
						start = cur;
						
						// Skip whitespace, just in case.
						SkipWhiteSpace(s, ref start, ref cur);
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
		
		/*    // Uncomment to diagnose token generation.
		Debug.Log(fileName);
		foreach (var T in Tokens)
		{
			Debug.Log($"{fileName} {T.text} {T.line}");
		}
		/* */

		//var linesOfCreateMember = new Dictionary<int>()
		var linesOfConstructors = new HashSet<int>();
		string className = "";
		int lineOfDisposeEnd = -1;
		var Enum = Tokens.GetEnumerator();
		bool isClass = false;
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
				isClass = Enum.Current.text == "class";
				
				Enum.MoveNext();
				
				if (!knownDisposables.ContainsKey(Enum.Current.text))
				{
					//Debug.Log($"{fileName}: {Enum.Current.text} did not match any known disposables!");
					return;
				}

				className = Enum.Current.text;

				Enum.MoveNext();

				//Debug.Log($"{Enum.Current.text}");
				if (Enum.Current.text == "<")
				{
					while (Enum.Current.text != ">")
					{
						Enum.MoveNext();
					}

					Enum.MoveNext();
				}

				Debug.Log($"'{Enum.Current.text}'");
				if (Enum.Current.text == ":")
				{
					do
					{
						Enum.MoveNext();
						//Debug.Log($"{Enum.Current.text}");

						if (Enum.Current.text == "IDisposable")
						{
							//Debug.Log($"{fileName} is Disposable, not IAutoDisposable!");
							return;
						}

						Enum.MoveNext();
						//Debug.Log($"{Enum.Current.text}");
					} while (Enum.Current.text == ",");
				}
				
				//Debug.Log($"{Enum.Current.text}");
				while (Enum.Current.text == "where")
				{
					Enum.MoveNext();
					string GenericName = Enum.Current.text;		// TYPE of where

					Enum.MoveNext();		// Should be :
					if (Enum.Current.text != ":")
					{
						Debug.LogError($"{fileName}: Expected : after where {GenericName}");
						return;
					}

					Enum.MoveNext();	// Name of type
					Enum.MoveNext();	// Next token is brace
					
					if (Enum.Current.text == "<")
					{
						while (Enum.Current.text != ">")
						{
							Enum.MoveNext();
						}

						Enum.MoveNext();
					}
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
						else if (Enum.Current.text == className)
						{
							while (Enum.Current.text != "{")
							{
								Enum.MoveNext();
							}

							linesOfConstructors.Add(Enum.Current.line);
							
							SkipBlock(ref Enum);
							Enum.MoveNext();
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
				else
				{
					Debug.LogError($"{fileName}: Unable to find class body for {className}, got {Enum.Current.text} at {Enum.Current.line} instead");
					return;
				}
			}
			else if (!Enum.MoveNext())
			{
				break;
			}
		}

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
						generated.Add($"\tprivate bool _meowInitialized_{disp.Name};");
					}

					if (isClass)
					{
						generated.Add("\tprotected virtual void Dispose(bool disposing)");
					}
					else
					{
						generated.Add("\tprivate void Dispose(bool disposing)");
					}

					generated.Add("\t{");
					foreach (var disp in knownDisposables[className].DisposableMembers)
					{
						generated.Add($"\t\tif (_meowInitialized_{disp.Name})");
						generated.Add("\t\t{");
						generated.Add($"\t\t\t{disp.Name}.Dispose();");
						generated.Add($"\t\t\t_meowInitialized_{disp.Name} = false;");
						generated.Add("\t\t}");
					}
					generated.Add("\t}");
					generated.Add("");
					if (isClass)
					{
						generated.Add($"\t~{className}() => Dispose(false);");
					}

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

				if (linesOfConstructors.Contains(i-1))
				{
					generated.Add("#region " + key);
					foreach (var disp in knownDisposables[className].DisposableMembers)
					{
						generated.Add($"\t\t_meowInitialized_{disp.Name} = false;");
					}

					generated.Add("#endregion " + key);
				}
			}

			if (generated.Count == allLines.Length)
			{
				bool bIsIdentical = true;
				for (int j = 0; j < generated.Count; j++)
				{
					if (generated[j] != allLines[j])
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


	private static string CleanName(string s)
	{
		return s.Contains("`") ? s.Split('`')[0] : s;
	}
	

	private static void DoGenerateCode()
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

					string className = CleanName(t.Name);
					//Debug.Log($"Adding disposible <{className}>");
					classData.type = t;
					classData.DisposableMembers = foundDisposables;
					AutoDisposables.Add(className, classData);
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
	

	private static void CompilationPipelineOncompilationStarted(object obj)
	{
		if (MeowCodeMenu.IsCodeGenEnable())
		{
			DoGenerateCode();
		}
	}

	public static void OnRegenerateCodeRequested()
	{
		DoGenerateCode();
	}

	public static void OnClearGeneratedCodeRequested()
	{
		
	}
}