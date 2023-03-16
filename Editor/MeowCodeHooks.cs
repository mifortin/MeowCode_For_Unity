//  This Source Code Form is subject to the terms of the Mozilla Public
//  License, v. 2.0. If a copy of the MPL was not distributed with this
//  file, You can obtain one at https://mozilla.org/MPL/2.0/. 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	private static string key = "/* --[GENERATED]-- */";
	private static char[] whitespace = { ' ', '\t',';' };
	private static HashSet<string> protection = new HashSet<string>{ "public", "protected", "private"};

	private static void ProcessFile(string fileName, HashSet<string> knownDisposables)
	{
		string[] allLines =File.ReadAllLines(fileName);

		var original = allLines.Where(s => !s.StartsWith(key)).ToList();
		var generated = new List<string>();

		var disposables = new List<string>();
		
		bool bAutoDispose = false;
		foreach (string s in original)
		{
			if (bAutoDispose && s.StartsWith("}"))
			{
				generated.Add(key + "	public void Dispose()");
				generated.Add(key + "	{");
				foreach (var d in disposables)
				{
					generated.Add(key + "		if ("+d+" != null)");
					generated.Add(key + "		{");
					generated.Add(key + "			" + d + ".Dispose();");
					generated.Add(key + "			" + d + " = null;");
					generated.Add(key + "		}");
				}
				generated.Add(key + "	}");
			}

			generated.Add(s);

			if (s.Contains("class"))
			{
				if (s.Contains("IAutoDisposable"))
				{
					bAutoDispose = true;
				}
				else
				{
					bAutoDispose = false;
				}
			}
			else
			{
				string[] parts = s.Split(whitespace, StringSplitOptions.RemoveEmptyEntries);

				bool bNextIsName = false;
				string foundName = "";
				foreach (string p in parts)
				{
					if (protection.Contains(p))	continue;

					if (bNextIsName)
					{
						if (foundName == "")
							foundName = p;
						else
						{
							if (p == "=")
								break;

							if (p.StartsWith("("))
							{
								foundName = "";
								break;
							}
						}
						continue;
					}
					
					if (p.Contains("("))
						break;

					string[] typeNameMaybe = p.Split('<');
					if (!knownDisposables.Contains(typeNameMaybe[0])) break;
					else bNextIsName = true;
					
				}

				if (foundName != "") disposables.Add(foundName);
			}
		}

		if (allLines.Length == generated.Count)
		{
			bool bAllTheSame = true;
			for (int i = 0; i < allLines.Length; i++)
			{
				if (allLines[i] != generated[i])
				{
					bAllTheSame = false;
					break;
				}
			}

			if (bAllTheSame)
			{
				return;
			}
		}
		
		Debug.Log($"{fileName} Generated lines: {generated.Count} ({allLines.Length}/{original.Count})");
		File.WriteAllLines(fileName, generated);
	}

	private static void CompilationPipelineOncompilationStarted(object obj)
	{
		HashSet<string> knownDisposables = new HashSet<string>();
		knownDisposables.Add("NativeArray");
		
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

			ProcessFile(F, knownDisposables);
		}
	}
}