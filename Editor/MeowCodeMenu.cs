//  This Source Code Form is subject to the terms of the Mozilla Public
//  License, v. 2.0. If a copy of the MPL was not distributed with this
//  file, You can obtain one at https://mozilla.org/MPL/2.0/. 

using UnityEditor;
using UnityEngine;

public class MeowCodeMenu : MonoBehaviour
{
	private static bool bEnableCodeGen = false;

	static public bool IsCodeGenEnable()
	{
		return bEnableCodeGen;
	}

	[MenuItem("MeowCode/Enable")]
	static void DoEnable()
	{
		bEnableCodeGen = true;
	}

	[MenuItem("MeowCode/Disable")]
	static void DoDisable()
	{
		bEnableCodeGen = false;
	}

	[MenuItem("MeowCode/Enable", true)]
	static bool OnValidateEnable()
	{
		return !bEnableCodeGen;
	}
	
	[MenuItem("MeowCode/Disable", true)]
	static bool OnValidateDisable()
	{
		return bEnableCodeGen;
	}
}
